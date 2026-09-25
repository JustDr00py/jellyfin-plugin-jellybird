using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Jellyfin.Plugin.Jellybird.Client.Models;

namespace Jellyfin.Plugin.Jellybird.Client;

/// <summary>
/// <see cref="IJellybirdClient"/> implementation backed by a named
/// <see cref="HttpClient"/> ("Jellybird", registered with no fixed
/// BaseAddress in <see cref="PluginServiceRegistrator"/>). Every call except
/// <see cref="CheckHealthAsync"/> reads the base URL/token from
/// <see cref="Plugin.Instance"/> at call time (not at construction), so
/// editing the config page takes effect immediately without a restart.
/// </summary>
public class JellybirdClient : IJellybirdClient
{
    private const string ClientName = "Jellybird";
    private static readonly TimeSpan TorrentsTimeout = TimeSpan.FromSeconds(40);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly Func<(string BaseUrl, string Token)> _configProvider;

    public JellybirdClient(IHttpClientFactory httpClientFactory)
        : this(httpClientFactory, DefaultConfigProvider)
    {
    }

    /// <summary>
    /// Test-only seam: lets unit tests supply base URL/token without a real
    /// <see cref="Plugin.Instance"/> (which needs live Jellyfin server
    /// dependencies to construct). Production code always uses the
    /// single-argument constructor, which reads live plugin configuration —
    /// this does not change that default behavior.
    /// </summary>
    internal JellybirdClient(IHttpClientFactory httpClientFactory, Func<(string BaseUrl, string Token)> configProvider)
    {
        _httpClientFactory = httpClientFactory;
        _configProvider = configProvider;
    }

    private static (string BaseUrl, string Token) DefaultConfigProvider()
    {
        var config = Plugin.Instance?.Configuration
            ?? throw new JellybirdApiException("jellybird plugin is not fully initialized yet");
        return (config.JellybirdBaseUrl, config.JellybirdToken);
    }

    public async Task<HealthzResponse> CheckHealthAsync(string baseUrl, string token, CancellationToken cancellationToken)
    {
        var result = await GetJsonAsync<HealthzResponse>("/healthz", baseUrl, token, null, cancellationToken).ConfigureAwait(false);
        return result!;
    }

    public async Task TriggerSyncAsync(CancellationToken cancellationToken)
    {
        var (baseUrl, token) = _configProvider();
        var client = _httpClientFactory.CreateClient(ClientName);
        using var request = new HttpRequestMessage(HttpMethod.Post, BuildUri(baseUrl, "/api/sync"));
        ApplyToken(request, token);
        using var response = await SendAsync(client, request, cancellationToken, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<SearchResult>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        var path = $"/api/search?q={Uri.EscapeDataString(query)}";
        var result = await GetJsonAsync<List<SearchResult>>(path, null, null, null, cancellationToken).ConfigureAwait(false);
        return result ?? [];
    }

    public async Task<IReadOnlyList<SeasonInfo>> GetSeasonsAsync(int tmdbId, CancellationToken cancellationToken)
    {
        var path = $"/api/tv/seasons?tmdb_id={tmdbId}";
        var result = await GetJsonAsync<List<SeasonInfo>>(path, null, null, null, cancellationToken).ConfigureAwait(false);
        return result ?? [];
    }

    public async Task<IReadOnlyList<EpisodeInfo>> GetEpisodesAsync(int tmdbId, int season, CancellationToken cancellationToken)
    {
        var path = $"/api/tv/episodes?tmdb_id={tmdbId}&season={season}";
        var result = await GetJsonAsync<List<EpisodeInfo>>(path, null, null, null, cancellationToken).ConfigureAwait(false);
        return result ?? [];
    }

    public async Task<IReadOnlyList<TorrentCandidate>> SearchTorrentsAsync(int tmdbId, string mediaType, int? season, int? episode, CancellationToken cancellationToken)
    {
        var path = $"/api/torrents?tmdb_id={tmdbId}&type={Uri.EscapeDataString(mediaType)}";
        if (season is not null)
        {
            path += $"&season={season}";
        }

        if (episode is not null)
        {
            path += $"&episode={episode}";
        }

        // Fans out to Torrentio + per-provider cache checks on jellybird's
        // side, which can take longer than the client's default timeout.
        var result = await GetJsonAsync<List<TorrentCandidate>>(path, null, null, TorrentsTimeout, cancellationToken).ConfigureAwait(false);
        return result ?? [];
    }

    public async Task<bool> ExistsAsync(string mediaType, string tmdbId, int? season, int? episode, CancellationToken cancellationToken)
    {
        var path = $"/api/library/check?type={Uri.EscapeDataString(mediaType)}&tmdb_id={Uri.EscapeDataString(tmdbId)}";
        if (season is not null)
        {
            path += $"&season={season}";
        }

        if (episode is not null)
        {
            path += $"&episode={episode}";
        }

        var result = await GetJsonAsync<ExistsResponse>(path, null, null, null, cancellationToken).ConfigureAwait(false);
        return result?.Exists ?? false;
    }

    public async Task<AddTorrentResponse> AddAsync(AddTorrentRequest request, CancellationToken cancellationToken)
    {
        return await SendJsonAsync<AddTorrentResponse>(HttpMethod.Post, "/api/add", request, cancellationToken).ConfigureAwait(false)
            ?? throw new JellybirdApiException("jellybird returned an empty response for /api/add");
    }

    public async Task<DiscoverPage> DiscoverAsync(string mediaType, string? list, int? genre, int page, CancellationToken cancellationToken)
    {
        var path = $"/api/discover?type={Uri.EscapeDataString(mediaType)}&page={page}";
        path += genre is > 0 ? $"&genre={genre}" : $"&list={Uri.EscapeDataString(list ?? "trending")}";
        var result = await GetJsonAsync<DiscoverPage>(path, null, null, null, cancellationToken).ConfigureAwait(false);
        return result ?? new DiscoverPage();
    }

    public async Task<DiscoverLists> GetDiscoverListsAsync(string mediaType, CancellationToken cancellationToken)
    {
        var path = $"/api/discover/lists?type={Uri.EscapeDataString(mediaType)}";
        var result = await GetJsonAsync<DiscoverLists>(path, null, null, null, cancellationToken).ConfigureAwait(false);
        return result ?? new DiscoverLists();
    }

    public async Task<IReadOnlyList<CloudTorrent>> ListCloudAsync(CancellationToken cancellationToken)
    {
        var result = await GetJsonAsync<List<CloudTorrent>>("/api/cloud", null, null, null, cancellationToken).ConfigureAwait(false);
        return result ?? [];
    }

    public async Task RemoveCloudAsync(string provider, string torrentId, CancellationToken cancellationToken)
    {
        var path = $"/api/cloud?provider={Uri.EscapeDataString(provider)}&id={Uri.EscapeDataString(torrentId)}";
        await SendJsonAsync<object>(HttpMethod.Delete, path, null, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<LocalFile>> ListLocalAsync(CancellationToken cancellationToken)
    {
        var result = await GetJsonAsync<List<LocalFile>>("/api/local", null, null, null, cancellationToken).ConfigureAwait(false);
        return result ?? [];
    }

    public async Task<KeepLocalResponse> KeepLocalAsync(LocalFileRef file, CancellationToken cancellationToken)
    {
        return await SendJsonAsync<KeepLocalResponse>(HttpMethod.Post, "/api/local", file, cancellationToken).ConfigureAwait(false)
            ?? new KeepLocalResponse();
    }

    public async Task RemoveLocalAsync(string provider, string torrentId, string fileId, CancellationToken cancellationToken)
    {
        var path = $"/api/local?provider={Uri.EscapeDataString(provider)}&torrent_id={Uri.EscapeDataString(torrentId)}&file_id={Uri.EscapeDataString(fileId)}";
        await SendJsonAsync<object>(HttpMethod.Delete, path, null, cancellationToken).ConfigureAwait(false);
    }

    public async Task MoveLocalAsync(LocalFileRef file, CancellationToken cancellationToken)
    {
        await SendJsonAsync<object>(HttpMethod.Post, "/api/local/move", file, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Sends a request with an optional JSON body using the saved configuration, returning the decoded response.</summary>
    private async Task<T?> SendJsonAsync<T>(HttpMethod method, string pathAndQuery, object? body, CancellationToken cancellationToken)
    {
        var (baseUrl, token) = _configProvider();
        var client = _httpClientFactory.CreateClient(ClientName);
        using var request = new HttpRequestMessage(method, BuildUri(baseUrl, pathAndQuery));
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, body.GetType(), options: JsonOptions);
        }

        ApplyToken(request, token);
        using var response = await SendAsync(client, request, cancellationToken, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        return await ReadJsonAsync<T>(response, cancellationToken).ConfigureAwait(false);
    }

    private async Task<T?> GetJsonAsync<T>(string pathAndQuery, string? explicitBaseUrl, string? explicitToken, TimeSpan? timeout, CancellationToken cancellationToken)
    {
        var (baseUrl, token) = explicitBaseUrl is null ? _configProvider() : (explicitBaseUrl, explicitToken ?? string.Empty);
        var client = _httpClientFactory.CreateClient(ClientName);
        using var request = new HttpRequestMessage(HttpMethod.Get, BuildUri(baseUrl, pathAndQuery));
        ApplyToken(request, token);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (timeout is not null)
        {
            cts.CancelAfter(timeout.Value);
        }

        using var response = await SendAsync(client, request, cts.Token, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        return await ReadJsonAsync<T>(response, cancellationToken).ConfigureAwait(false);
    }

    private static Uri BuildUri(string baseUrl, string pathAndQuery)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new JellybirdApiException("jellybird base URL is not configured — set it on the plugin's settings page");
        }

        var trimmed = baseUrl.TrimEnd('/');
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var baseUri) ||
            (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
        {
            throw new JellybirdApiException($"jellybird base URL '{baseUrl}' is not a valid http/https URL");
        }

        return new Uri(trimmed + pathAndQuery, UriKind.Absolute);
    }

    private static void ApplyToken(HttpRequestMessage request, string token)
    {
        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Add("X-Jellybird-Token", token);
        }
    }

    /// <summary>
    /// Sends the request, normalizing any transport-level failure (DNS,
    /// connection refused, timeout) into <see cref="JellybirdApiException"/>.
    /// <paramref name="sendToken"/> is what's passed to SendAsync (may be a
    /// linked/timeout token); <paramref name="callerToken"/> is the original
    /// token from the plugin's own caller, used to tell "the caller cancelled
    /// us" apart from "jellybird didn't respond in time".
    /// </summary>
    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpRequestMessage request, CancellationToken sendToken, CancellationToken callerToken)
    {
        try
        {
            return await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, sendToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            if (callerToken.IsCancellationRequested)
            {
                throw; // the caller (Jellyfin) cancelled the request itself — propagate as-is, don't mask it.
            }

            throw new JellybirdApiException(
                $"jellybird is unreachable at the configured base URL ({ex.Message})", statusCode: null, inner: ex);
        }
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        string message = $"jellybird returned {(int)response.StatusCode} {response.StatusCode}";
        try
        {
            var body = await response.Content.ReadFromJsonAsync<ErrorBody>(JsonOptions, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(body?.Error))
            {
                message = body!.Error;
            }
        }
        catch (JsonException)
        {
            // Non-JSON error body (e.g. a reverse proxy's HTML error page) — keep the generic status message.
        }

        throw new JellybirdApiException(message, (int)response.StatusCode);
    }

    private static async Task<T?> ReadJsonAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.StatusCode == HttpStatusCode.NoContent)
        {
            return default;
        }

        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken).ConfigureAwait(false);
    }

    private sealed class ErrorBody
    {
        public string? Error { get; set; }
    }
}
