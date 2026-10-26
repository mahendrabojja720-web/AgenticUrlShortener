namespace AgenticUrlShortener.Orchestration.Stages;

using AgenticUrlShortener.Orchestration.Core;

/// <summary>
/// Stage: Validate URL access and update analytics.
/// Entry gate: Short code must exist.
/// Exit gate: Analytics updated, redirect URL ready.
/// </summary>
public class ValidateAndUpdateAccessStage : WorkflowStage
{
    private readonly Infrastructure.Persistence.IUrlRepository _repository;

    public ValidateAndUpdateAccessStage(Infrastructure.Persistence.IUrlRepository repository)
    {
        _repository = repository;
        StageName = "ValidateAndUpdateAccess";
        DependsOn = new List<string>();
        RequiresApproval = false;
    }

    public override async Task<StageExecutionResult> ExecuteAsync(WorkflowExecutionContext context)
    {
        try
        {
            if (!context.Execution.Context.TryGetValue("short_code", out var codeObj))
            {
                return new StageExecutionResult
                {
                    Success = false,
                    ErrorMessage = "Short code not provided"
                };
            }

            var shortCode = (string)codeObj;
            var url = await _repository.GetByShortCodeAsync(shortCode);

            if (url == null)
            {
                context.LogAudit($"Short code not found: {shortCode}");
                return new StageExecutionResult
                {
                    Success = false,
                    ErrorMessage = "Short URL not found"
                };
            }

            // Check expiration
            if (url.ExpiresAt.HasValue && url.ExpiresAt.Value < DateTime.UtcNow)
            {
                url.IsActive = false;
                await _repository.UpdateAsync(url);
                context.LogAudit($"Short URL expired: {shortCode}");
                return new StageExecutionResult
                {
                    Success = false,
                    ErrorMessage = "Short URL has expired"
                };
            }

            // Update access metrics
            url.AccessCount++;
            url.LastAccessedAt = DateTime.UtcNow;
            await _repository.UpdateAsync(url);

            context.SharedContext["original_url"] = url.OriginalUrl;
            context.SharedContext["url_entity"] = url;
            context.LogAudit($"Access recorded for short code: {shortCode}, Total accesses: {url.AccessCount}");

            return new StageExecutionResult
            {
                Success = true,
                Output = new Dictionary<string, object>
                {
                    { "original_url", url.OriginalUrl },
                    { "access_count", url.AccessCount }
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
}

/// <summary>
/// Stage: Prepare redirect response.
/// Entry gate: Original URL validated and available.
/// Exit gate: Redirect ready.
/// </summary>
public class PrepareRedirectStage : WorkflowStage
{
    public PrepareRedirectStage()
    {
        StageName = "PrepareRedirect";
        DependsOn = new List<string> { "ValidateAndUpdateAccess" };
        RequiresApproval = false;
    }

    public override async Task<StageExecutionResult> ExecuteAsync(WorkflowExecutionContext context)
    {
        return await Task.Run(() =>
        {
            try
            {
                if (!context.SharedContext.TryGetValue("original_url", out var urlObj))
                {
                    return new StageExecutionResult
                    {
                        Success = false,
                        ErrorMessage = "Original URL not found in context"
                    };
                }

                var originalUrl = (string)urlObj;
                context.SharedContext["redirect_url"] = originalUrl;
                context.LogAudit($"Redirect prepared to: {originalUrl}");

                return new StageExecutionResult
                {
                    Success = true,
                    Output = new Dictionary<string, object>
                    {
                        { "redirect_url", originalUrl }
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
/// Stage: Delete a shortened URL (brownfield scenario).
/// Entry gate: URL ID provided and exists.
/// Exit gate: URL deleted from repository.
/// </summary>
public class DeleteUrlStage : WorkflowStage
{
    private readonly Infrastructure.Persistence.IUrlRepository _repository;

    public DeleteUrlStage(Infrastructure.Persistence.IUrlRepository repository)
    {
        _repository = repository;
        StageName = "DeleteUrl";
        DependsOn = new List<string>();
        RequiresApproval = true; // Deletion requires approval for safety
    }

    public override async Task<StageExecutionResult> ExecuteAsync(WorkflowExecutionContext context)
    {
        try
        {
            if (!context.Execution.Context.TryGetValue("url_id", out var idObj))
            {
                return new StageExecutionResult
                {
                    Success = false,
                    ErrorMessage = "URL ID not provided"
                };
            }

            var urlId = (string)idObj;
            var url = await _repository.GetByIdAsync(urlId);

            if (url == null)
            {
                return new StageExecutionResult
                {
                    Success = false,
                    ErrorMessage = $"URL not found: {urlId}"
                };
            }

            // Store for rollback
            context.SharedContext["deleted_url"] = url;

            await _repository.DeleteAsync(urlId);
            context.LogAudit($"URL deleted: {urlId} (Code: {url.ShortCode})");

            return new StageExecutionResult
            {
                Success = true,
                Output = new Dictionary<string, object>
                {
                    { "deleted_id", urlId },
                    { "deleted_code", url.ShortCode }
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
        // Restore deleted URL on rollback
        if (context.SharedContext.TryGetValue("deleted_url", out var urlObj))
        {
            var url = (Domain.Models.ShortUrl)urlObj;
            await _repository.CreateAsync(url);
            context.LogAudit($"URL restored on rollback: {url.Id}");
        }
        return true;
    }
}
