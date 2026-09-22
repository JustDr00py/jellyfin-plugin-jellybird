using Jellyfin.Plugin.Jellybird.Client.Models;

namespace Jellyfin.Plugin.Jellybird.Client;

/// <summary>Thin HTTP client over jellybird's REST API. All methods throw <see cref="JellybirdApiException"/> on failure.</summary>
public interface IJellybirdClient
{
    /// <summary>Calls GET /healthz against an explicit base URL/token (not necessarily saved config yet) — used by "Test Connection".</summary>
    Task<HealthzResponse> CheckHealthAsync(string baseUrl, string token, CancellationToken cancellationToken);

    /// <summary>Fires POST /api/sync using the saved plugin configuration. Returns once jellybird acknowledges (202); does not wait for the sync itself to finish.</summary>
    Task TriggerSyncAsync(CancellationToken cancellationToken);

    /// <summary>GET /api/search?q=</summary>
    Task<IReadOnlyList<SearchResult>> SearchAsync(string query, CancellationToken cancellationToken);

    /// <summary>GET /api/tv/seasons?tmdb_id=</summary>
    Task<IReadOnlyList<SeasonInfo>> GetSeasonsAsync(int tmdbId, CancellationToken cancellationToken);

    /// <summary>GET /api/tv/episodes?tmdb_id=&amp;season=</summary>
    Task<IReadOnlyList<EpisodeInfo>> GetEpisodesAsync(int tmdbId, int season, CancellationToken cancellationToken);

    /// <summary>GET /api/torrents?tmdb_id=&amp;type=&amp;season=&amp;episode= (season/episode only for TV).</summary>
    Task<IReadOnlyList<TorrentCandidate>> SearchTorrentsAsync(int tmdbId, string mediaType, int? season, int? episode, CancellationToken cancellationToken);

    /// <summary>POST /api/add</summary>
    Task<AddTorrentResponse> AddAsync(AddTorrentRequest request, CancellationToken cancellationToken);
}
