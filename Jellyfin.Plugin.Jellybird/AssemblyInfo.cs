using System.Runtime.CompilerServices;

// Lets the test project construct JellybirdClient via its internal
// test-only constructor overload (see Client/JellybirdClient.cs) without
// making that seam part of the plugin's public API surface.
[assembly: InternalsVisibleTo("Jellyfin.Plugin.Jellybird.Tests")]
