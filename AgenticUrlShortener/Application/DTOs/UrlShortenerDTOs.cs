namespace AgenticUrlShortener.Application.DTOs;

public class CreateShortUrlRequest
{
    public string OriginalUrl { get; set; } = string.Empty;
    public string? CustomShortCode { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string? CreatedBy { get; set; }
}

public class CreateShortUrlResponse
{
    public string Id { get; set; } = string.Empty;
    public string ShortCode { get; set; } = string.Empty;
    public string ShortUrl { get; set; } = string.Empty;
    public string OriginalUrl { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
}

public class GetShortUrlResponse
{
    public string Id { get; set; } = string.Empty;
    public string OriginalUrl { get; set; } = string.Empty;
    public string ShortCode { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool IsActive { get; set; }
    public int AccessCount { get; set; }
    public DateTime? LastAccessedAt { get; set; }
}

public class AnalyticsResponse
{
    public string ShortUrlId { get; set; } = string.Empty;
    public string ShortCode { get; set; } = string.Empty;
    public int TotalAccess { get; set; }
    public DateTime? LastAccessedAt { get; set; }
    public string? LastAccessedFrom { get; set; }
    public Dictionary<string, int> ReferrerCounts { get; set; } = new();
}
