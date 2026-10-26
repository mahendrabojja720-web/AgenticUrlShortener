namespace AgenticUrlShortener.Infrastructure.Persistence;

using AgenticUrlShortener.Domain.Models;
using System.Collections.Concurrent;

/// <summary>
/// In-memory data store for URL shortener entities. 
/// Suitable for prototyping; replace with persistent database in production.
/// </summary>
public interface IUrlRepository
{
    Task<ShortUrl?> GetByShortCodeAsync(string shortCode);
    Task<ShortUrl?> GetByIdAsync(string id);
    Task<ShortUrl> CreateAsync(ShortUrl shortUrl);
    Task UpdateAsync(ShortUrl shortUrl);
    Task DeleteAsync(string id);
    Task<List<ShortUrl>> GetAllAsync();
}

public class InMemoryUrlRepository : IUrlRepository
{
    private readonly ConcurrentDictionary<string, ShortUrl> _urls = new();
    private readonly object _lock = new object();

    public Task<ShortUrl?> GetByShortCodeAsync(string shortCode)
    {
        var url = _urls.Values.FirstOrDefault(u => u.ShortCode == shortCode && u.IsActive);
        return Task.FromResult(url);
    }

    public Task<ShortUrl?> GetByIdAsync(string id)
    {
        _urls.TryGetValue(id, out var url);
        return Task.FromResult(url);
    }

    public Task<ShortUrl> CreateAsync(ShortUrl shortUrl)
    {
        lock (_lock)
        {
            shortUrl.Id = Guid.NewGuid().ToString();
            _urls.TryAdd(shortUrl.Id, shortUrl);
            return Task.FromResult(shortUrl);
        }
    }

    public Task UpdateAsync(ShortUrl shortUrl)
    {
        lock (_lock)
        {
            if (_urls.TryGetValue(shortUrl.Id, out _))
            {
                _urls[shortUrl.Id] = shortUrl;
            }
        }
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string id)
    {
        lock (_lock)
        {
            _urls.TryRemove(id, out _);
        }
        return Task.CompletedTask;
    }

    public Task<List<ShortUrl>> GetAllAsync()
    {
        return Task.FromResult(_urls.Values.ToList());
    }
}
