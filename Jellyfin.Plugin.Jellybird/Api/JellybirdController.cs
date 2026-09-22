using Jellyfin.Plugin.Jellybird.Client;
using Jellyfin.Plugin.Jellybird.Client.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.Jellybird.Api;

/// <summary>
/// Thin server-side proxy for the Jellybird Search dashboard page. Every
/// endpoint here just forwards to jellybird's own REST API and translates
/// errors into a consistent shape for the frontend — no business logic
/// lives here. Admin-only: adding content spends the user's own debrid
/// quota/storage.
/// </summary>
/// <remarks>
/// "RequiresElevation" is the policy name Jellyfin's own admin-only plugin
/// endpoints use. It's referenced here by string rather than a constant
/// because this environment has no .NET SDK to build against and confirm
/// which namespace currently exports it (it has moved between
/// Jellyfin.Api.Constants and MediaBrowser.Common.Api across server
/// versions) — verify the exact constant to use once building against the
/// real Jellyfin.Controller 12.0.0 package, or leave the string as-is if
/// that's still valid.
/// </remarks>
[ApiController]
[Route("Jellybird")]
[Authorize(Policy = "RequiresElevation")]
public class JellybirdController : ControllerBase
{
    private readonly IJellybirdClient _client;

    public JellybirdController(IJellybirdClient client)
    {
        _client = client;
    }

    /// <summary>
    /// Tests connectivity to jellybird using the *currently typed*
    /// baseUrl/token (not necessarily saved yet), so the config page can
    /// validate before Save. Hits jellybird's unauthenticated /healthz.
    /// </summary>
    [HttpGet("TestConnection")]
    public async Task<ActionResult<HealthzResponse>> TestConnection([FromQuery] string baseUrl, [FromQuery] string? token, CancellationToken cancellationToken)
    {
        return await Invoke(() => _client.CheckHealthAsync(baseUrl, token ?? string.Empty, cancellationToken)).ConfigureAwait(false);
    }

    [HttpGet("Search")]
    public async Task<ActionResult<IReadOnlyList<SearchResult>>> Search([FromQuery] string q, CancellationToken cancellationToken)
    {
        return await Invoke(() => _client.SearchAsync(q, cancellationToken)).ConfigureAwait(false);
    }

    [HttpGet("Tv/Seasons")]
    public async Task<ActionResult<IReadOnlyList<SeasonInfo>>> TvSeasons([FromQuery] int tmdbId, CancellationToken cancellationToken)
    {
        return await Invoke(() => _client.GetSeasonsAsync(tmdbId, cancellationToken)).ConfigureAwait(false);
    }

    [HttpGet("Tv/Episodes")]
    public async Task<ActionResult<IReadOnlyList<EpisodeInfo>>> TvEpisodes([FromQuery] int tmdbId, [FromQuery] int season, CancellationToken cancellationToken)
    {
        return await Invoke(() => _client.GetEpisodesAsync(tmdbId, season, cancellationToken)).ConfigureAwait(false);
    }

    [HttpGet("Torrents")]
    public async Task<ActionResult<IReadOnlyList<TorrentCandidate>>> Torrents(
        [FromQuery] int tmdbId,
        [FromQuery] string type,
        [FromQuery] int? season,
        [FromQuery] int? episode,
        CancellationToken cancellationToken)
    {
        return await Invoke(() => _client.SearchTorrentsAsync(tmdbId, type, season, episode, cancellationToken)).ConfigureAwait(false);
    }

    /// <summary>"Is this TMDB title already in the library" — season/episode are ignored for movies, required for tv.</summary>
    [HttpGet("Library/Check")]
    public async Task<ActionResult<bool>> LibraryCheck(
        [FromQuery] int tmdbId,
        [FromQuery] string type,
        [FromQuery] int? season,
        [FromQuery] int? episode,
        CancellationToken cancellationToken)
    {
        return await Invoke(() => _client.ExistsAsync(type, tmdbId.ToString(System.Globalization.CultureInfo.InvariantCulture), season, episode, cancellationToken)).ConfigureAwait(false);
    }

    [HttpPost("Add")]
    public async Task<ActionResult<AddTorrentResponse>> Add([FromBody] AddTorrentRequest request, CancellationToken cancellationToken)
    {
        return await Invoke(() => _client.AddAsync(request, cancellationToken)).ConfigureAwait(false);
    }

    /// <summary>
    /// Runs a client call and maps <see cref="JellybirdApiException"/> to a
    /// consistent <c>{"error": "..."}</c> JSON body so the frontend never
    /// has to special-case network failures vs. jellybird-reported errors.
    /// </summary>
    private async Task<ActionResult<T>> Invoke<T>(Func<Task<T>> call)
    {
        try
        {
            return await call().ConfigureAwait(false);
        }
        catch (JellybirdApiException ex) when (ex.IsUnreachable)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { error = ex.Message });
        }
        catch (JellybirdApiException ex) when (ex.IsUnauthorized)
        {
            return StatusCode(StatusCodes.Status401Unauthorized, new { error = "jellybird rejected the token — check plugin settings" });
        }
        catch (JellybirdApiException ex)
        {
            return StatusCode(ex.StatusCode ?? StatusCodes.Status502BadGateway, new { error = ex.Message });
        }
    }
}
