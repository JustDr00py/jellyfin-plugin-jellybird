using System.Net;
using Jellyfin.Plugin.Jellybird.Client;
using Jellyfin.Plugin.Jellybird.Client.Models;
using Xunit;

namespace Jellyfin.Plugin.Jellybird.Tests;

/// <summary>Discover, Cloud and Local-files calls.</summary>
public class JellybirdClientBrowseTests
{
    private static JellybirdClient MakeClient(HttpMessageHandler handler)
    {
        return new JellybirdClient(new FakeHttpClientFactory(handler), () => ("http://jellybird:8097", "s3cret"));
    }

    // The request (and its content) is disposed once the client returns, so
    // tests that check a body capture it while the fake handler runs.
    private static FakeHttpMessageHandler Capturing(string json, Action<string?> onBody)
    {
        return new FakeHttpMessageHandler(req =>
        {
            onBody(req.Content?.ReadAsStringAsync().GetAwaiter().GetResult());
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
            };
        });
    }

    [Fact]
    public async Task DiscoverAsync_UsesList_AndDeserializesLibraryStatus()
    {
        const string json = """
            {"page":2,"total_pages":40,"results":[
              {"id":95396,"name":"Severance","media_type":"tv","first_air_date":"2022-02-18","vote_average":8.4,"in_library":true,"episodes":3}]}
            """;
        var handler = FakeHttpMessageHandler.Json(HttpStatusCode.OK, json);
        var client = MakeClient(handler);

        var page = await client.DiscoverAsync("tv", "top_rated", null, 2, CancellationToken.None);

        Assert.Equal("http://jellybird:8097/api/discover?type=tv&page=2&list=top_rated", handler.LastRequest!.RequestUri!.ToString());
        Assert.Equal(40, page.TotalPages);
        var item = Assert.Single(page.Results);
        Assert.True(item.InLibrary);
        Assert.Equal(3, item.Episodes);
        Assert.Equal(8.4, item.VoteAverage);
        Assert.Equal("Severance", item.DisplayTitle);
    }

    [Fact]
    public async Task DiscoverAsync_GenreReplacesList()
    {
        var handler = FakeHttpMessageHandler.Json(HttpStatusCode.OK, """{"page":1,"total_pages":1,"results":[]}""");
        var client = MakeClient(handler);

        await client.DiscoverAsync("movie", "popular", 878, 1, CancellationToken.None);

        Assert.Equal("http://jellybird:8097/api/discover?type=movie&page=1&genre=878", handler.LastRequest!.RequestUri!.ToString());
    }

    [Fact]
    public async Task GetDiscoverListsAsync_DeserializesListsAndGenres()
    {
        const string json = """{"lists":[{"name":"trending","label":"Trending"}],"genres":[{"id":18,"name":"Drama"}]}""";
        var handler = FakeHttpMessageHandler.Json(HttpStatusCode.OK, json);
        var client = MakeClient(handler);

        var lists = await client.GetDiscoverListsAsync("tv", CancellationToken.None);

        Assert.Equal("http://jellybird:8097/api/discover/lists?type=tv", handler.LastRequest!.RequestUri!.ToString());
        Assert.Equal("Trending", Assert.Single(lists.Lists).Label);
        Assert.Equal(18, Assert.Single(lists.Genres).Id);
    }

    [Fact]
    public async Task ListCloudAsync_DeserializesTorrents()
    {
        const string json = """[{"provider":"realdebrid","id":"ABC","name":"Dune.2021.2160p","status":"ready","files":2,"size":123456789}]""";
        var client = MakeClient(FakeHttpMessageHandler.Json(HttpStatusCode.OK, json));

        var torrents = await client.ListCloudAsync(CancellationToken.None);

        var t = Assert.Single(torrents);
        Assert.Equal("ABC", t.Id);
        Assert.Equal("ready", t.Status);
        Assert.Equal(123456789, t.Size);
    }

    [Fact]
    public async Task RemoveCloudAsync_SendsDelete_WithEscapedQuery()
    {
        var handler = FakeHttpMessageHandler.Json(HttpStatusCode.OK, """{"status":"removed"}""");
        var client = MakeClient(handler);

        await client.RemoveCloudAsync("torbox", "12 34&x", CancellationToken.None);

        Assert.Equal(HttpMethod.Delete, handler.LastRequest!.Method);
        Assert.Equal("/api/cloud?provider=torbox&id=12%2034%26x", handler.LastRequest!.RequestUri!.PathAndQuery);
        Assert.Equal("s3cret", handler.LastRequest!.Headers.GetValues("X-Jellybird-Token").Single());
    }

    [Fact]
    public async Task RemoveCloudAsync_MapsJellybirdError()
    {
        var handler = FakeHttpMessageHandler.Json(HttpStatusCode.BadGateway, """{"error":"realdebrid: HTTP 404"}""");
        var client = MakeClient(handler);

        var ex = await Assert.ThrowsAsync<JellybirdApiException>(() => client.RemoveCloudAsync("realdebrid", "X", CancellationToken.None));

        Assert.Equal(502, ex.StatusCode);
        Assert.Equal("realdebrid: HTTP 404", ex.Message);
    }

    [Fact]
    public async Task ListLocalAsync_DeserializesCopies()
    {
        const string json = """
            [{"provider":"realdebrid","torrent_id":"T1","file_id":"1","torrent_name":"Dune","file_path":"Dune.mkv",
              "size_bytes":100,"bytes_done":40,"status":"downloading","local_path":"","error":"","in_library":false,"move_to":"/nas/Movies"}]
            """;
        var client = MakeClient(FakeHttpMessageHandler.Json(HttpStatusCode.OK, json));

        var list = await client.ListLocalAsync(CancellationToken.None);

        var lf = Assert.Single(list);
        Assert.Equal("downloading", lf.Status);
        Assert.Equal(40, lf.BytesDone);
        Assert.False(lf.InLibrary);
        Assert.Equal("/nas/Movies", lf.MoveTo);
    }

    [Fact]
    public async Task KeepLocalAsync_WholeTorrent_OmitsFileId()
    {
        string? body = null;
        var handler = Capturing("""{"queued":2,"files":3}""", b => body = b);
        var client = MakeClient(handler);

        var res = await client.KeepLocalAsync(new LocalFileRef { Provider = "realdebrid", TorrentId = "T1" }, CancellationToken.None);

        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Equal("/api/local", handler.LastRequest!.RequestUri!.PathAndQuery);
        Assert.Equal("""{"provider":"realdebrid","torrent_id":"T1"}""", body);
        Assert.Equal(2, res.Queued);
        Assert.Equal(3, res.Files);
    }

    [Fact]
    public async Task RemoveLocalAsync_SendsDelete_WithAllIds()
    {
        var handler = FakeHttpMessageHandler.Json(HttpStatusCode.OK, """{"status":"removed"}""");
        var client = MakeClient(handler);

        await client.RemoveLocalAsync("realdebrid", "T1", "7", CancellationToken.None);

        Assert.Equal(HttpMethod.Delete, handler.LastRequest!.Method);
        Assert.Equal("/api/local?provider=realdebrid&torrent_id=T1&file_id=7", handler.LastRequest!.RequestUri!.PathAndQuery);
    }

    [Fact]
    public async Task MoveLocalAsync_PostsFileRef()
    {
        string? body = null;
        var handler = Capturing("""{"status":"moving"}""", b => body = b);
        var client = MakeClient(handler);

        await client.MoveLocalAsync(new LocalFileRef { Provider = "torbox", TorrentId = "T1", FileId = "2" }, CancellationToken.None);

        Assert.Equal("/api/local/move", handler.LastRequest!.RequestUri!.PathAndQuery);
        Assert.Equal("""{"provider":"torbox","torrent_id":"T1","file_id":"2"}""", body);
    }
}
