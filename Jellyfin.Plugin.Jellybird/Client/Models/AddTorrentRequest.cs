using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.Jellybird.Client.Models;

/// <summary>Request body for jellybird's POST /api/add.</summary>
public class AddTorrentRequest
{
    /// <summary>Required; must start with "magnet:".</summary>
    [JsonPropertyName("magnet")]
    public string Magnet { get; set; } = string.Empty;

    [JsonPropertyName("info_hash")]
    public string? InfoHash { get; set; }

    /// <summary>Optional preferred provider: "realdebrid" or "torbox".</summary>
    [JsonPropertyName("provider")]
    public string? Provider { get; set; }

    /// <summary>Optional naming hint so jellybird files the resulting .strm under the right title.</summary>
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("year")]
    public int? Year { get; set; }

    /// <summary>"movie" or "tv".</summary>
    [JsonPropertyName("media_type")]
    public string? MediaType { get; set; }

    [JsonPropertyName("season")]
    public int? Season { get; set; }

    [JsonPropertyName("episode")]
    public int? Episode { get; set; }
}
