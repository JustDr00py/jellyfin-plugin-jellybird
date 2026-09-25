using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.Jellybird.Client.Models;

/// <summary>One server-side ("Keep local") copy, as returned by jellybird's GET /api/local.</summary>
public class LocalFile
{
    [JsonPropertyName("provider")]
    public string Provider { get; set; } = string.Empty;

    [JsonPropertyName("torrent_id")]
    public string TorrentId { get; set; } = string.Empty;

    [JsonPropertyName("file_id")]
    public string FileId { get; set; } = string.Empty;

    [JsonPropertyName("torrent_name")]
    public string TorrentName { get; set; } = string.Empty;

    [JsonPropertyName("file_path")]
    public string FilePath { get; set; } = string.Empty;

    [JsonPropertyName("size_bytes")]
    public long SizeBytes { get; set; }

    [JsonPropertyName("bytes_done")]
    public long BytesDone { get; set; }

    /// <summary>queued, downloading, moving, done or failed.</summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("local_path")]
    public string LocalPath { get; set; } = string.Empty;

    [JsonPropertyName("error")]
    public string Error { get; set; } = string.Empty;

    /// <summary>False once the file left the debrid cloud: this copy is then the only one left.</summary>
    [JsonPropertyName("in_library")]
    public bool InLibrary { get; set; }

    /// <summary>Set for finished copies outside the download folder: where a move would put them.</summary>
    [JsonPropertyName("move_to")]
    public string? MoveTo { get; set; }
}
