using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.Jellybird.Client.Models;

/// <summary>Response body from jellybird's POST /api/local.</summary>
public class KeepLocalResponse
{
    /// <summary>Files queued by this request (files already local or in progress are skipped).</summary>
    [JsonPropertyName("queued")]
    public int Queued { get; set; }

    /// <summary>Library files the request matched.</summary>
    [JsonPropertyName("files")]
    public int Files { get; set; }
}
