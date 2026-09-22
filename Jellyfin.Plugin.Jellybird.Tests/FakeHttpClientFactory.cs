namespace Jellyfin.Plugin.Jellybird.Tests;

/// <summary>Always returns an HttpClient wrapping a single fixed handler — enough for these tests, which create one client per case.</summary>
internal sealed class FakeHttpClientFactory : IHttpClientFactory
{
    private readonly HttpMessageHandler _handler;

    public FakeHttpClientFactory(HttpMessageHandler handler)
    {
        _handler = handler;
    }

    public HttpClient CreateClient(string name) => new(_handler, disposeHandler: false);
}
