namespace Jellyfin.Plugin.Jellybird.Client;

/// <summary>
/// Raised for any failure calling jellybird's API — a non-success HTTP status
/// (message taken from jellybird's own <c>{"error": "..."}</c> body when present),
/// or a network-level failure (connection refused, DNS failure, timeout) normalized
/// to a single "unreachable" case. Callers only ever need to catch this one type.
/// </summary>
public class JellybirdApiException : Exception
{
    /// <summary>
    /// HTTP status code from jellybird, or null when the request never reached it
    /// (network failure, timeout, or a local configuration problem like a missing
    /// base URL).
    /// </summary>
    public int? StatusCode { get; }

    /// <summary>True when jellybird responded 401 — the configured token is wrong (or jellybird now requires one).</summary>
    public bool IsUnauthorized => StatusCode == 401;

    /// <summary>True when the request never reached jellybird at all.</summary>
    public bool IsUnreachable => StatusCode is null;

    public JellybirdApiException(string message, int? statusCode = null, Exception? inner = null)
        : base(message, inner)
    {
        StatusCode = statusCode;
    }
}
