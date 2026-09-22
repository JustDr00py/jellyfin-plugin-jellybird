using System.Net;
using Jellyfin.Plugin.Jellybird.Client;
using Jellyfin.Plugin.Jellybird.Client.Models;
using Xunit;

namespace Jellyfin.Plugin.Jellybird.Tests;

public class JellybirdClientTests
{
    private static JellybirdClient MakeClient(HttpMessageHandler handler, string baseUrl = "http://jellybird:8097", string token = "s3cret")
    {
        return new JellybirdClient(new FakeHttpClientFactory(handler), () => (baseUrl, token));
    }

    [Fact]
    public async Task SearchAsync_SetsTokenHeader_WhenTokenNonEmpty()
    {
        var handler = FakeHttpMessageHandler.Json(HttpStatusCode.OK, "[]");
        var client = MakeClient(handler, token: "s3cret");

        await client.SearchAsync("dune", CancellationToken.None);

        Assert.True(handler.LastRequest!.Headers.TryGetValues("X-Jellybird-Token", out var values));
        Assert.Equal("s3cret", values!.Single());
    }

    [Fact]
    public async Task SearchAsync_OmitsTokenHeader_WhenTokenEmpty()
    {
        var handler = FakeHttpMessageHandler.Json(HttpStatusCode.OK, "[]");
        var client = MakeClient(handler, token: "");

        await client.SearchAsync("dune", CancellationToken.None);

        Assert.False(handler.LastRequest!.Headers.Contains("X-Jellybird-Token"));
    }

    [Fact]
    public async Task SearchAsync_StripsTrailingSlash_FromBaseUrl()
    {
        var handler = FakeHttpMessageHandler.Json(HttpStatusCode.OK, "[]");
        var client = MakeClient(handler, baseUrl: "http://jellybird:8097/");

        await client.SearchAsync("dune", CancellationToken.None);

        Assert.Equal("http://jellybird:8097/api/search?q=dune", handler.LastRequest!.RequestUri!.ToString());
    }

    [Fact]
    public async Task SearchAsync_DeserializesHappyPath()
    {
        const string json = """
            [{"id":603,"title":"The Matrix","name":"","original_title":"The Matrix","original_name":"",
              "release_date":"1999-03-30","first_air_date":"","media_type":"movie","overview":"...", "poster_path":"/x.jpg"}]
            """;
        var handler = FakeHttpMessageHandler.Json(HttpStatusCode.OK, json);
        var client = MakeClient(handler);

        var results = await client.SearchAsync("matrix", CancellationToken.None);

        var r = Assert.Single(results);
        Assert.Equal(603, r.Id);
        Assert.Equal("The Matrix", r.Title);
        Assert.Equal("movie", r.MediaType);
        Assert.Equal(1999, r.Year);
        Assert.Equal("The Matrix", r.DisplayTitle);
    }

    [Fact]
    public async Task GetSeasonsAsync_DeserializesHappyPath()
    {
        const string json = """[{"season_number":2,"name":"Season 2","episode_count":10,"poster_path":"/s2.jpg","air_date":"2022-01-01"}]""";
        var client = MakeClient(FakeHttpMessageHandler.Json(HttpStatusCode.OK, json));

        var seasons = await client.GetSeasonsAsync(1399, CancellationToken.None);

        var s = Assert.Single(seasons);
        Assert.Equal(2, s.SeasonNumber);
        Assert.Equal(10, s.EpisodeCount);
    }

    [Fact]
    public async Task GetEpisodesAsync_DeserializesHappyPath()
    {
        const string json = """[{"episode_number":1,"name":"Pilot","overview":"...","still_path":"/e1.jpg","air_date":"2022-01-01"}]""";
        var client = MakeClient(FakeHttpMessageHandler.Json(HttpStatusCode.OK, json));

        var episodes = await client.GetEpisodesAsync(1399, 2, CancellationToken.None);

        var e = Assert.Single(episodes);
        Assert.Equal(1, e.EpisodeNumber);
        Assert.Equal("Pilot", e.Name);
    }

    [Fact]
    public async Task SearchTorrentsAsync_DeserializesHappyPath_AndBuildsExpectedQuery()
    {
        const string json = """
            [{"title":"Dune.Part.Two.2024.2160p","size_bytes":8000000000,"seeders":42,"source":"torrentio",
              "provider":"realdebrid","hash":"abc","magnet":"magnet:?xt=urn:btih:abc","cached":true}]
            """;
        var handler = FakeHttpMessageHandler.Json(HttpStatusCode.OK, json);
        var client = MakeClient(handler);

        var results = await client.SearchTorrentsAsync(693134, "movie", null, null, CancellationToken.None);

        var t = Assert.Single(results);
        Assert.True(t.Cached);
        Assert.Equal("realdebrid", t.Provider);
        Assert.Equal("http://jellybird:8097/api/torrents?tmdb_id=693134&type=movie", handler.LastRequest!.RequestUri!.ToString());
    }

    [Fact]
    public async Task SearchTorrentsAsync_IncludesSeasonAndEpisode_ForTv()
    {
        var handler = FakeHttpMessageHandler.Json(HttpStatusCode.OK, "[]");
        var client = MakeClient(handler);

        await client.SearchTorrentsAsync(1399, "tv", 2, 1, CancellationToken.None);

        Assert.Equal("http://jellybird:8097/api/torrents?tmdb_id=1399&type=tv&season=2&episode=1", handler.LastRequest!.RequestUri!.ToString());
    }

    [Fact]
    public async Task AddAsync_DeserializesHappyPath()
    {
        const string json = """{"provider":"realdebrid","torrent_id":"abc123","cached":true,"synced":true}""";
        var client = MakeClient(FakeHttpMessageHandler.Json(HttpStatusCode.OK, json));

        var result = await client.AddAsync(new AddTorrentRequest { Magnet = "magnet:?xt=urn:btih:abc" }, CancellationToken.None);

        Assert.Equal("realdebrid", result.Provider);
        Assert.Equal("abc123", result.TorrentId);
        Assert.True(result.Cached);
        Assert.True(result.Synced);
    }

    [Fact]
    public async Task TriggerSyncAsync_TreatsAccepted_AsSuccess_WithoutParsingBody()
    {
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Accepted)
        {
            Content = new StringContent("""{"status":"syncing"}"""),
        });
        var client = MakeClient(handler);

        await client.TriggerSyncAsync(CancellationToken.None); // must not throw

        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Equal("http://jellybird:8097/api/sync", handler.LastRequest!.RequestUri!.ToString());
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task SearchAsync_MapsErrorStatus_ToJellybirdApiException(HttpStatusCode status)
    {
        var handler = FakeHttpMessageHandler.Json(status, """{"error":"boom"}""");
        var client = MakeClient(handler);

        var ex = await Assert.ThrowsAsync<JellybirdApiException>(() => client.SearchAsync("x", CancellationToken.None));

        Assert.Equal((int)status, ex.StatusCode);
        Assert.Equal("boom", ex.Message);
        Assert.Equal(status == HttpStatusCode.Unauthorized, ex.IsUnauthorized);
        Assert.False(ex.IsUnreachable);
    }

    [Fact]
    public async Task SearchAsync_MapsConnectionFailure_ToUnreachable()
    {
        var handler = FakeHttpMessageHandler.Throwing(new HttpRequestException("connection refused"));
        var client = MakeClient(handler);

        var ex = await Assert.ThrowsAsync<JellybirdApiException>(() => client.SearchAsync("x", CancellationToken.None));

        Assert.True(ex.IsUnreachable);
        Assert.Null(ex.StatusCode);
    }

    [Fact]
    public async Task SearchAsync_MapsTimeout_ToUnreachable()
    {
        var handler = FakeHttpMessageHandler.Throwing(new TaskCanceledException("timed out", new TimeoutException()));
        var client = MakeClient(handler);

        var ex = await Assert.ThrowsAsync<JellybirdApiException>(() => client.SearchAsync("x", CancellationToken.None));

        Assert.True(ex.IsUnreachable);
    }

    [Fact]
    public async Task SearchAsync_ThrowsConfigError_WhenBaseUrlMissing()
    {
        var handler = FakeHttpMessageHandler.Json(HttpStatusCode.OK, "[]");
        var client = MakeClient(handler, baseUrl: "");

        var ex = await Assert.ThrowsAsync<JellybirdApiException>(() => client.SearchAsync("x", CancellationToken.None));

        Assert.True(ex.IsUnreachable); // never reached the network — no status code
        Assert.Contains("base URL", ex.Message);
    }

    [Fact]
    public async Task CheckHealthAsync_UsesExplicitBaseUrlAndToken_NotSavedConfig()
    {
        var handler = FakeHttpMessageHandler.Json(HttpStatusCode.OK, """{"status":"ok","version":"1.2.3"}""");
        // Saved config points elsewhere; CheckHealthAsync must ignore it and use the explicit args (config-page "Test Connection" flow tests unsaved values).
        var client = MakeClient(handler, baseUrl: "http://saved-config:8097", token: "saved-token");

        var health = await client.CheckHealthAsync("http://typed-not-yet-saved:8097", "typed-token", CancellationToken.None);

        Assert.Equal("ok", health.Status);
        Assert.Equal("1.2.3", health.Version);
        Assert.Equal("http://typed-not-yet-saved:8097/healthz", handler.LastRequest!.RequestUri!.ToString());
        Assert.Equal("typed-token", handler.LastRequest!.Headers.GetValues("X-Jellybird-Token").Single());
    }
}
