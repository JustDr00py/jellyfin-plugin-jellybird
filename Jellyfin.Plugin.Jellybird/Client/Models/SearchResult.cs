using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.Jellybird.Client.Models;

/// <summary>One TMDB multi-search result, as returned by jellybird's GET /api/search.</summary>
public class SearchResult
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    /// <summary>Movie title (empty for TV results — use <see cref="Name"/> instead).</summary>
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    /// <summary>TV show name (empty for movie results — use <see cref="Title"/> instead).</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("original_title")]
    public string? OriginalTitle { get; set; }

    [JsonPropertyName("original_name")]
    public string? OriginalName { get; set; }

    [JsonPropertyName("release_date")]
    public string? ReleaseDate { get; set; }

    [JsonPropertyName("first_air_date")]
    public string? FirstAirDate { get; set; }

    [JsonPropertyName("media_type")]
    public string MediaType { get; set; } = string.Empty;

    [JsonPropertyName("overview")]
    public string? Overview { get; set; }

    [JsonPropertyName("poster_path")]
    public string? PosterPath { get; set; }

    /// <summary>Display title regardless of media type.</summary>
    public string DisplayTitle => MediaType == "tv" ? (Name ?? OriginalName ?? string.Empty) : (Title ?? OriginalTitle ?? string.Empty);

    /// <summary>Four-digit year parsed from whichever date field applies, or null.</summary>
    public int? Year
    {
        get
        {
            var date = MediaType == "tv" ? FirstAirDate : ReleaseDate;
            return !string.IsNullOrEmpty(date) && date.Length >= 4 && int.TryParse(date.AsSpan(0, 4), out var y) ? y : null;
        }
    }
}
