using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.Jellybird.Client.Models;

/// <summary>Identifies a library file (or, with no <see cref="FileId"/>, every file of a torrent) for the local-copy endpoints.</summary>
public class LocalFileRef
{
    [JsonPropertyName("provider")]
    public string Provider { get; set; } = string.Empty;

    [JsonPropertyName("torrent_id")]
    public string TorrentId { get; set; } = string.Empty;

    [JsonPropertyName("file_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? FileId { get; set; }
}
