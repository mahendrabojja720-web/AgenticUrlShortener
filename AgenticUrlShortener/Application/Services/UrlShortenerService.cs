namespace AgenticUrlShortener.Application.Services;

using AgenticUrlShortener.Domain.Models;
using AgenticUrlShortener.Application.DTOs;

/// <summary>
/// Core service for URL shortening operations with validation and analytics.
/// </summary>
public interface IUrlShortenerService
{
    Task<CreateShortUrlResponse> CreateShortUrlAsync(CreateShortUrlRequest request);
    Task<GetShortUrlResponse?> GetShortUrlAsync(string shortCode);
    Task<AnalyticsResponse?> GetAnalyticsAsync(string shortCode);
    Task<bool> DeleteShortUrlAsync(string id);
    Task<List<GetShortUrlResponse>> ListUrlsAsync();
}

public class UrlShortenerService : IUrlShortenerService
{
    private readonly Infrastructure.Persistence.IUrlRepository _repository;
    private const int MaxShortCodeLength = 8;
    private const int MinShortCodeLength = 4;
    private static readonly string ValidChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    public UrlShortenerService(Infrastructure.Persistence.IUrlRepository repository)
    {
        _repository = repository;
    }

    public async Task<CreateShortUrlResponse> CreateShortUrlAsync(CreateShortUrlRequest request)
    {
        ValidateUrl(request.OriginalUrl);

        var shortCode = string.IsNullOrWhiteSpace(request.CustomShortCode)
            ? GenerateShortCode()
            : request.CustomShortCode;

        // Validate uniqueness
        var existing = await _repository.GetByShortCodeAsync(shortCode);
        if (existing != null)
        {
            throw new InvalidOperationException($"Short code '{shortCode}' already exists.");
        }

        var shortUrl = new ShortUrl
        {
            OriginalUrl = request.OriginalUrl,
            ShortCode = shortCode,
            ExpiresAt = request.ExpiresAt,
            CreatedBy = request.CreatedBy,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
            AccessCount = 0
        };

        var created = await _repository.CreateAsync(shortUrl);

        return new CreateShortUrlResponse
        {
            Id = created.Id,
            ShortCode = created.ShortCode,
            ShortUrl = $"https://short.url/{created.ShortCode}",
            OriginalUrl = created.OriginalUrl,
            CreatedAt = created.CreatedAt,
            ExpiresAt = created.ExpiresAt
        };
    }

    public async Task<GetShortUrlResponse?> GetShortUrlAsync(string shortCode)
    {
        var url = await _repository.GetByShortCodeAsync(shortCode);
        if (url == null)
        {
            return null;
        }

        // Check expiration
        if (url.ExpiresAt.HasValue && url.ExpiresAt.Value < DateTime.UtcNow)
        {
            url.IsActive = false;
            await _repository.UpdateAsync(url);
            return null;
        }

        // Update access metrics
        url.AccessCount++;
        url.LastAccessedAt = DateTime.UtcNow;
        await _repository.UpdateAsync(url);

        return new GetShortUrlResponse
        {
            Id = url.Id,
            OriginalUrl = url.OriginalUrl,
            ShortCode = url.ShortCode,
            CreatedAt = url.CreatedAt,
            ExpiresAt = url.ExpiresAt,
            IsActive = url.IsActive,
            AccessCount = url.AccessCount,
            LastAccessedAt = url.LastAccessedAt
        };
    }

    public async Task<AnalyticsResponse?> GetAnalyticsAsync(string shortCode)
    {
        var url = await _repository.GetByShortCodeAsync(shortCode);
        if (url == null)
        {
            return null;
        }

        return new AnalyticsResponse
        {
            ShortUrlId = url.Id,
            ShortCode = url.ShortCode,
            TotalAccess = url.AccessCount,
            LastAccessedAt = url.LastAccessedAt
        };
    }

    public async Task<bool> DeleteShortUrlAsync(string id)
    {
        var url = await _repository.GetByIdAsync(id);
        if (url == null)
        {
            return false;
        }

        await _repository.DeleteAsync(id);
        return true;
    }

    public async Task<List<GetShortUrlResponse>> ListUrlsAsync()
    {
        var all = await _repository.GetAllAsync();
        return all
            .Where(u => u.IsActive && (!u.ExpiresAt.HasValue || u.ExpiresAt > DateTime.UtcNow))
            .Select(u => new GetShortUrlResponse
            {
                Id = u.Id,
                OriginalUrl = u.OriginalUrl,
                ShortCode = u.ShortCode,
                CreatedAt = u.CreatedAt,
                ExpiresAt = u.ExpiresAt,
                IsActive = u.IsActive,
                AccessCount = u.AccessCount,
                LastAccessedAt = u.LastAccessedAt
            })
            .ToList();
    }

    private static void ValidateUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new ArgumentException("URL cannot be empty.");
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("URL must be a valid HTTP or HTTPS URL.");
        }

        if (url.Length > 2048)
        {
            throw new ArgumentException("URL exceeds maximum length of 2048 characters.");
        }
    }

    private static string GenerateShortCode()
    {
        var random = new Random();
        var length = random.Next(MinShortCodeLength, MaxShortCodeLength + 1);
        return new string(Enumerable.Range(0, length)
            .Select(_ => ValidChars[random.Next(ValidChars.Length)])
            .ToArray());
    }
}
