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

    /// <summary>GET /api/library/check?type=&amp;tmdb_id=&amp;season=&amp;episode= — "is this TMDB title already in the library". Season/episode are ignored for movies.</summary>
    Task<bool> ExistsAsync(string mediaType, string tmdbId, int? season, int? episode, CancellationToken cancellationToken);

    /// <summary>GET /api/discover?type=&amp;list=|genre=&amp;page= — one page of a TMDB list, labelled with library status. A genre overrides the list.</summary>
    Task<DiscoverPage> DiscoverAsync(string mediaType, string? list, int? genre, int page, CancellationToken cancellationToken);

    /// <summary>GET /api/discover/lists?type= — the lists and genres Discover offers.</summary>
    Task<DiscoverLists> GetDiscoverListsAsync(string mediaType, CancellationToken cancellationToken);

    /// <summary>GET /api/cloud — every torrent in every enabled debrid cloud.</summary>
    Task<IReadOnlyList<CloudTorrent>> ListCloudAsync(CancellationToken cancellationToken);

    /// <summary>DELETE /api/cloud?provider=&amp;id= — delete a torrent from the debrid cloud (and its .strm files).</summary>
    Task RemoveCloudAsync(string provider, string torrentId, CancellationToken cancellationToken);

    /// <summary>GET /api/local — server-side copies and their download status.</summary>
    Task<IReadOnlyList<LocalFile>> ListLocalAsync(CancellationToken cancellationToken);

    /// <summary>POST /api/local — queue a file (or a whole torrent, with no file id) for a server-side copy; also retries failed copies.</summary>
    Task<KeepLocalResponse> KeepLocalAsync(LocalFileRef file, CancellationToken cancellationToken);

    /// <summary>DELETE /api/local?provider=&amp;torrent_id=&amp;file_id= — cancel a copy or delete it from the server.</summary>
    Task RemoveLocalAsync(string provider, string torrentId, string fileId, CancellationToken cancellationToken);

    /// <summary>POST /api/local/move — move a finished copy into the download folder, in the background.</summary>
    Task MoveLocalAsync(LocalFileRef file, CancellationToken cancellationToken);
}
