namespace AgenticUrlShortener.Domain.Models;

/// <summary>
/// Represents usage analytics for a shortened URL.
/// </summary>
public class UrlAnalytics
{
    public string Id { get; set; } = string.Empty;
    public string ShortUrlId { get; set; } = string.Empty;
    public int TotalAccess { get; set; }
    public DateTime LastAccessedAt { get; set; }
    public string? LastAccessedFrom { get; set; }
    public string? LastAccessedUserAgent { get; set; }
    public Dictionary<string, int> ReferrerCounts { get; set; } = new();
    public Dictionary<string, int> CountryDistribution { get; set; } = new();
}
