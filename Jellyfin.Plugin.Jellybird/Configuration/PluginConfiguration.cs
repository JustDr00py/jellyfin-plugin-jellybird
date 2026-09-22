using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.Jellybird.Configuration;

/// <summary>Admin-configurable settings, edited via <c>configPage.html</c>.</summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Base URL of the jellybird instance, e.g. "http://jellybird:8097" (Docker
    /// compose network) or "http://192.168.1.50:8097" (LAN IP). No scheme/host
    /// is assumed — the admin must set this explicitly. Stored without a
    /// trailing slash.
    /// </summary>
    public string JellybirdBaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Shared token jellybird expects on /api/* and /stream/* requests (its
    /// own <c>server.token</c> config). Sent as the X-Jellybird-Token header.
    /// Leave empty if jellybird's own token is unset (auth disabled).
    /// </summary>
    public string JellybirdToken { get; set; } = string.Empty;

    /// <summary>
    /// Optional UI convenience: pre-selects the provider dropdown on the
    /// search/add page. "", "realdebrid", or "torbox". jellybird's own
    /// /api/add already treats the provider as an optional preference.
    /// </summary>
    public string DefaultProvider { get; set; } = string.Empty;
}
