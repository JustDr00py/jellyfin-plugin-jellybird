using System.Globalization;
using Jellyfin.Plugin.Jellybird.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.Jellybird;

/// <summary>
/// Connector plugin for jellybird (https://github.com/JustDr00py/jellybird).
/// Owns no library data of its own — it's a thin front door that lets an
/// admin trigger a jellybird sync and search/add debrid content from inside
/// Jellyfin, entirely by calling jellybird's existing REST API.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    /// <inheritdoc />
    public override string Name => "Jellybird";

    /// <inheritdoc />
    public override Guid Id => Guid.Parse("23ba68d8-75e0-4f79-b5ea-348ad0e3214f");

    /// <inheritdoc />
    public override string Description =>
        "Trigger jellybird syncs, discover and add debrid content, and manage your cloud and local copies without leaving Jellyfin.";

    /// <summary>
    /// Singleton access so <see cref="Client.JellybirdClient"/>, the
    /// scheduled task, and the controller can all read live configuration
    /// without re-injecting <see cref="PluginConfiguration"/> everywhere.
    /// </summary>
    public static Plugin? Instance { get; private set; }

    /// <inheritdoc />
    /// <remarks>
    /// Two findings shaped this, both confirmed against real, currently-
    /// working Jellyfin plugin source (not guessed):
    ///
    /// 1. Only one page gets EnableInMainMenu. A second PluginPageInfo entry
    ///    with EnableInMainMenu was tried first (for a dedicated "Jellybird
    ///    Search" sidebar item) and confirmed broken: Jellyfin only wires
    ///    EnableInMainMenu through its SPA router for a plugin's one true
    ///    settings page; any second such page loads as a raw, un-bootstrapped
    ///    document with no ApiClient/Dashboard globals. Confirmed against
    ///    n00bcodr/Jellyfin-Enhanced, whose extra pages go through a
    ///    different mechanism (GetViews(), rendered inside Jellyfin's user-
    ///    preferences screens) instead. So the search/add UI lives as a
    ///    second tab inside this one page (configPage.html) rather than its
    ///    own sidebar entry.
    ///
    /// 2. A plugin page's inline &lt;script&gt; tag does not reliably execute.
    ///    Jellyfin's view loader (jellyfin-web's viewContainer.js) only runs
    ///    inline scripts by inserting the view via jQuery's appendTo(), and
    ///    only when window.$ is jQuery — not guaranteed on this install. The
    ///    officially supported mechanism instead is data-controller: the
    ///    root page element declares data-controller="__plugin/&lt;Name&gt;",
    ///    and Jellyfin dynamic-imports the matching PluginPageInfo's
    ///    EmbeddedResourcePath as an ES module, calling its default export
    ///    with the view element. Confirmed against jellyfin/jellyfin-plugin-
    ///    fanart (an official Jellyfin-org plugin) — its fanart.html/
    ///    fanart.js pair is the exact template this plugin's configPage.html
    ///    / jellybird.js follow.
    /// </remarks>
    public IEnumerable<PluginPageInfo> GetPages()
    {
        return
        [
            new PluginPageInfo
            {
                Name = Name,
                EmbeddedResourcePath = string.Format(CultureInfo.InvariantCulture, "{0}.Configuration.configPage.html", GetType().Namespace),
                EnableInMainMenu = true,
                MenuIcon = "cloud_download",
            },
            new PluginPageInfo
            {
                // Name must match the data-controller="__plugin/jellybirdjs"
                // attribute on configPage.html's root page element.
                Name = "jellybirdjs",
                EmbeddedResourcePath = string.Format(CultureInfo.InvariantCulture, "{0}.Configuration.jellybird.js", GetType().Namespace),
            },
        ];
    }
}
