using Jellyfin.Plugin.Jellybird.Client;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.Jellybird;

/// <summary>Registers the named HttpClient and <see cref="IJellybirdClient"/> for DI.</summary>
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        // Named (not typed) client with no fixed BaseAddress: JellybirdClient
        // reads the base URL/token from Plugin.Instance.Configuration on
        // every call, so editing the config page takes effect immediately
        // without needing to rebuild a typed client or restart Jellyfin.
        serviceCollection.AddHttpClient("Jellybird", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(20);
        });

        serviceCollection.AddSingleton<IJellybirdClient, JellybirdClient>();
    }
}
