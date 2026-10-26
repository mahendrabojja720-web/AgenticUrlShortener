namespace AgenticUrlShortener.Tests.Services;

using Xunit;
using AgenticUrlShortener.Application.Services;
using AgenticUrlShortener.Application.DTOs;
using AgenticUrlShortener.Infrastructure.Persistence;

public class UrlShortenerServiceTests
{
    private readonly IUrlShortenerService _service;
    private readonly IUrlRepository _repository;

    public UrlShortenerServiceTests()
    {
        _repository = new InMemoryUrlRepository();
        _service = new UrlShortenerService(_repository);
    }

    [Fact]
    public async Task CreateShortUrl_WithValidUrl_ReturnsSuccessResponse()
    {
        // Arrange
        var request = new CreateShortUrlRequest
        {
            OriginalUrl = "https://www.example.com/very/long/url",
            CreatedBy = "test-user"
        };

        // Act
        var response = await _service.CreateShortUrlAsync(request);

        // Assert
        Assert.NotNull(response);
        Assert.NotEmpty(response.ShortCode);
        Assert.Equal("https://www.example.com/very/long/url", response.OriginalUrl);
        Assert.NotEmpty(response.Id);
    }

    [Fact]
    public async Task CreateShortUrl_WithCustomCode_UseCustomCode()
    {
        // Arrange
        var request = new CreateShortUrlRequest
        {
            OriginalUrl = "https://www.example.com",
            CustomShortCode = "mycode123"
        };

        // Act
        var response = await _service.CreateShortUrlAsync(request);

        // Assert
        Assert.Equal("mycode123", response.ShortCode);
    }

    [Fact]
    public async Task CreateShortUrl_WithInvalidUrl_ThrowsException()
    {
        // Arrange
        var request = new CreateShortUrlRequest
        {
            OriginalUrl = "not a valid url"
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateShortUrlAsync(request));
    }

    [Fact]
    public async Task CreateShortUrl_WithDuplicateCode_ThrowsException()
    {
        // Arrange
        var request1 = new CreateShortUrlRequest
        {
            OriginalUrl = "https://www.example.com",
            CustomShortCode = "unique123"
        };
        var request2 = new CreateShortUrlRequest
        {
            OriginalUrl = "https://www.another.com",
            CustomShortCode = "unique123"
        };

        // Act
        await _service.CreateShortUrlAsync(request1);

        // Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.CreateShortUrlAsync(request2));
    }

    [Fact]
    public async Task GetShortUrl_WithValidCode_ReturnsUrl()
    {
        // Arrange
        var createRequest = new CreateShortUrlRequest
        {
            OriginalUrl = "https://www.example.com"
        };
        var created = await _service.CreateShortUrlAsync(createRequest);

        // Act
        var response = await _service.GetShortUrlAsync(created.ShortCode);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(created.OriginalUrl, response.OriginalUrl);
        Assert.Equal(1, response.AccessCount);
    }

    [Fact]
    public async Task GetShortUrl_WithInvalidCode_ReturnsNull()
    {
        // Act
        var response = await _service.GetShortUrlAsync("nonexistent");

        // Assert
        Assert.Null(response);
    }

    [Fact]
    public async Task GetShortUrl_WithExpiredUrl_ReturnsNull()
    {
        // Arrange
        var request = new CreateShortUrlRequest
        {
            OriginalUrl = "https://www.example.com",
            ExpiresAt = DateTime.UtcNow.AddHours(-1)
        };
        var created = await _service.CreateShortUrlAsync(request);

        // Act
        var response = await _service.GetShortUrlAsync(created.ShortCode);

        // Assert
        Assert.Null(response);
    }

    [Fact]
    public async Task GetAnalytics_WithValidCode_ReturnsAnalytics()
    {
        // Arrange
        var createRequest = new CreateShortUrlRequest
        {
            OriginalUrl = "https://www.example.com"
        };
        var created = await _service.CreateShortUrlAsync(createRequest);

        // Access multiple times
        await _service.GetShortUrlAsync(created.ShortCode);
        await _service.GetShortUrlAsync(created.ShortCode);

        // Act
        var analytics = await _service.GetAnalyticsAsync(created.ShortCode);

        // Assert
        Assert.NotNull(analytics);
        Assert.Equal(2, analytics.TotalAccess);
    }

    [Fact]
    public async Task DeleteShortUrl_WithValidId_DeletesSuccessfully()
    {
        // Arrange
        var request = new CreateShortUrlRequest
        {
            OriginalUrl = "https://www.example.com"
        };
        var created = await _service.CreateShortUrlAsync(request);

        // Act
        var success = await _service.DeleteShortUrlAsync(created.Id);
        var response = await _service.GetShortUrlAsync(created.ShortCode);

        // Assert
        Assert.True(success);
        Assert.Null(response);
    }

    [Fact]
    public async Task ListUrls_ReturnsAllActiveUrls()
    {
        // Arrange
        await _service.CreateShortUrlAsync(new CreateShortUrlRequest
        {
            OriginalUrl = "https://www.example1.com"
        });
        await _service.CreateShortUrlAsync(new CreateShortUrlRequest
        {
            OriginalUrl = "https://www.example2.com"
        });

        // Act
        var urls = await _service.ListUrlsAsync();

        // Assert
        Assert.Equal(2, urls.Count);
    }
}
