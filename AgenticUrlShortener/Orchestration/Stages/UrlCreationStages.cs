namespace AgenticUrlShortener.Orchestration.Stages;

using AgenticUrlShortener.Orchestration.Core;
using AgenticUrlShortener.Application.Services;
using AgenticUrlShortener.Application.DTOs;

/// <summary>
/// Stage: Validate and normalize the URL creation request.
/// Entry gate: Request must be valid.
/// Exit gate: Normalized request in context.
/// </summary>
public class ValidateAndNormalizeUrlStage : WorkflowStage
{
    public ValidateAndNormalizeUrlStage()
    {
        StageName = "ValidateAndNormalize";
        DependsOn = new List<string>();
        RequiresApproval = false;
    }

    public override async Task<StageExecutionResult> ExecuteAsync(WorkflowExecutionContext context)
    {
        return await Task.Run(() =>
        {
            try
            {
                if (!context.Execution.Context.TryGetValue("request", out var requestObj))
                {
                    return new StageExecutionResult
                    {
                        Success = false,
                        ErrorMessage = "Request not found in context"
                    };
                }

                var request = requestObj as CreateShortUrlRequest;
                if (request == null)
                {
                    return new StageExecutionResult
                    {
                        Success = false,
                        ErrorMessage = "Invalid request format"
                    };
                }

                // Validate URL
                if (string.IsNullOrWhiteSpace(request.OriginalUrl))
                {
                    return new StageExecutionResult
                    {
                        Success = false,
                        ErrorMessage = "Original URL cannot be empty"
                    };
                }

                if (!Uri.TryCreate(request.OriginalUrl, UriKind.Absolute, out var uri) ||
                    (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                {
                    return new StageExecutionResult
                    {
                        Success = false,
                        ErrorMessage = "Invalid URL format"
                    };
                }

                // Normalize URL
                var normalizedUrl = uri.AbsoluteUri;
                request.OriginalUrl = normalizedUrl;

                // Store normalized request
                context.SharedContext["normalized_request"] = request;
                context.LogAudit($"URL validated and normalized: {normalizedUrl}");

                return new StageExecutionResult
                {
                    Success = true,
                    Output = new Dictionary<string, object>
                    {
                        { "normalized_url", normalizedUrl },
                        { "custom_code", request.CustomShortCode ?? "auto" }
                    }
                };
            }
            catch (Exception ex)
            {
                return new StageExecutionResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        });
    }
}

/// <summary>
/// Stage: Check for duplicates and reserve short code.
/// Entry gate: Normalized request available.
/// Exit gate: Short code reserved.
/// </summary>
public class CheckDuplicateAndReserveStage : WorkflowStage
{
    private readonly Infrastructure.Persistence.IUrlRepository _repository;
    private static readonly HashSet<string> _reservedCodes = new();
    private static readonly object _reserveLock = new object();

    public CheckDuplicateAndReserveStage(Infrastructure.Persistence.IUrlRepository repository)
    {
        _repository = repository;
        StageName = "CheckDuplicateAndReserve";
        DependsOn = new List<string> { "ValidateAndNormalize" };
        RequiresApproval = false;
    }

    public override async Task<StageExecutionResult> ExecuteAsync(WorkflowExecutionContext context)
    {
        try
        {
            if (!context.SharedContext.TryGetValue("normalized_request", out var requestObj))
            {
                return new StageExecutionResult
                {
                    Success = false,
                    ErrorMessage = "Normalized request not found"
                };
            }

            var request = (CreateShortUrlRequest)requestObj;

            // Check for duplicate URL
            var existing = await _repository.GetAllAsync();
            var duplicate = existing.FirstOrDefault(u =>
                u.OriginalUrl == request.OriginalUrl && u.IsActive &&
                (!u.ExpiresAt.HasValue || u.ExpiresAt > DateTime.UtcNow));

            if (duplicate != null)
            {
                context.LogAudit($"Duplicate URL detected: {duplicate.ShortCode}");
                return new StageExecutionResult
                {
                    Success = true,
                    Output = new Dictionary<string, object>
                    {
                        { "short_code", duplicate.ShortCode },
                        { "is_duplicate", true },
                        { "existing_id", duplicate.Id }
                    }
                };
            }

            // Reserve short code
            var shortCode = string.IsNullOrWhiteSpace(request.CustomShortCode)
                ? GenerateShortCode()
                : request.CustomShortCode;

            lock (_reserveLock)
            {
                if (_reservedCodes.Contains(shortCode))
                {
                    return new StageExecutionResult
                    {
                        Success = false,
                        ErrorMessage = $"Short code '{shortCode}' is reserved or in use"
                    };
                }
                _reservedCodes.Add(shortCode);
            }

            context.SharedContext["reserved_code"] = shortCode;
            context.LogAudit($"Short code reserved: {shortCode}");

            return new StageExecutionResult
            {
                Success = true,
                Output = new Dictionary<string, object>
                {
                    { "short_code", shortCode },
                    { "is_duplicate", false }
                }
            };
        }
        catch (Exception ex)
        {
            return new StageExecutionResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public override async Task<bool> RollbackAsync(WorkflowExecutionContext context)
    {
        // Release reserved code
        if (context.SharedContext.TryGetValue("reserved_code", out var codeObj))
        {
            lock (_reserveLock)
            {
                _reservedCodes.Remove((string)codeObj);
            }
            context.LogAudit($"Short code released on rollback: {codeObj}");
        }
        return await Task.FromResult(true);
    }

    private static string GenerateShortCode()
    {
        const string validChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var random = new Random();
        var length = random.Next(4, 9);
        return new string(Enumerable.Range(0, length)
            .Select(_ => validChars[random.Next(validChars.Length)])
            .ToArray());
    }
}

/// <summary>
/// Stage: Persist the shortened URL to the repository.
/// Entry gate: Reserved code available.
/// Exit gate: URL persisted with ID.
/// </summary>
public class PersistUrlStage : WorkflowStage
{
    private readonly Infrastructure.Persistence.IUrlRepository _repository;

    public PersistUrlStage(Infrastructure.Persistence.IUrlRepository repository)
    {
        _repository = repository;
        StageName = "PersistUrl";
        DependsOn = new List<string> { "CheckDuplicateAndReserve" };
        RequiresApproval = false;
    }

    public override async Task<StageExecutionResult> ExecuteAsync(WorkflowExecutionContext context)
    {
        try
        {
            if (!context.SharedContext.TryGetValue("normalized_request", out var requestObj) ||
                !context.SharedContext.TryGetValue("reserved_code", out var codeObj))
            {
                return new StageExecutionResult
                {
                    Success = false,
                    ErrorMessage = "Required context missing (request or code)"
                };
            }

            var request = (CreateShortUrlRequest)requestObj;
            var shortCode = (string)codeObj;

            var shortUrl = new Domain.Models.ShortUrl
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
            context.SharedContext["created_url"] = created;
            context.LogAudit($"URL persisted: ID={created.Id}, Code={shortCode}");

            return new StageExecutionResult
            {
                Success = true,
                Output = new Dictionary<string, object>
                {
                    { "url_id", created.Id },
                    { "short_code", created.ShortCode }
                }
            };
        }
        catch (Exception ex)
        {
            return new StageExecutionResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public override async Task<bool> RollbackAsync(WorkflowExecutionContext context)
    {
        // Delete persisted URL on rollback
        if (context.SharedContext.TryGetValue("created_url", out var urlObj))
        {
            var url = (Domain.Models.ShortUrl)urlObj;
            await _repository.DeleteAsync(url.Id);
            context.LogAudit($"URL deleted on rollback: {url.Id}");
        }
        return true;
    }
}

/// <summary>
/// Stage: Generate response and prepare output.
/// Entry gate: URL persisted.
/// Exit gate: Response ready for client.
/// </summary>
public class GenerateResponseStage : WorkflowStage
{
    public GenerateResponseStage()
    {
        StageName = "GenerateResponse";
        DependsOn = new List<string> { "PersistUrl" };
        RequiresApproval = false;
    }

    public override async Task<StageExecutionResult> ExecuteAsync(WorkflowExecutionContext context)
    {
        return await Task.Run(() =>
        {
            try
            {
                if (!context.SharedContext.TryGetValue("created_url", out var urlObj))
                {
                    return new StageExecutionResult
                    {
                        Success = false,
                        ErrorMessage = "Created URL not found in context"
                    };
                }

                var url = (Domain.Models.ShortUrl)urlObj;

                var response = new CreateShortUrlResponse
                {
                    Id = url.Id,
                    ShortCode = url.ShortCode,
                    ShortUrl = $"https://short.url/{url.ShortCode}",
                    OriginalUrl = url.OriginalUrl,
                    CreatedAt = url.CreatedAt,
                    ExpiresAt = url.ExpiresAt
                };

                context.SharedContext["response"] = response;
                context.LogAudit($"Response generated for short code: {url.ShortCode}");

                return new StageExecutionResult
                {
                    Success = true,
                    Output = new Dictionary<string, object>
                    {
                        { "short_url", response.ShortUrl },
                        { "short_code", response.ShortCode }
                    }
                };
            }
            catch (Exception ex)
            {
                return new StageExecutionResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        });
    }
}
