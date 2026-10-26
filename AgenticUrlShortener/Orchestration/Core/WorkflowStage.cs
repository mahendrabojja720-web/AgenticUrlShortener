namespace AgenticUrlShortener.Orchestration.Core;

using AgenticUrlShortener.Domain.Models;

/// <summary>
/// Defines a stage in the workflow orchestration pipeline.
/// Each stage has dependencies, execution logic, entry/exit gates, and rollback capability.
/// </summary>
public abstract class WorkflowStage
{
    public string StageName { get; protected set; } = string.Empty;
    public List<string> DependsOn { get; protected set; } = new();
    public bool RequiresApproval { get; protected set; }

    /// <summary>
    /// Execute stage logic with context and governance.
    /// </summary>
    public abstract Task<StageExecutionResult> ExecuteAsync(WorkflowExecutionContext context);

    /// <summary>
    /// Validate preconditions before execution.
    /// </summary>
    public virtual Task<(bool Valid, string? Reason)> ValidateEntryGatesAsync(WorkflowExecutionContext context)
    {
        // Verify all dependencies are completed
        foreach (var dep in DependsOn)
        {
            var depStage = context.Execution.Stages.FirstOrDefault(s => s.StageName == dep);
            if (depStage?.Status != StageStatus.Completed)
            {
                return Task.FromResult((false, $"Dependency '{dep}' not completed"));
            }
        }
        return Task.FromResult((true, (string?)null));
    }

    /// <summary>
    /// Rollback stage execution on failure or request.
    /// </summary>
    public virtual Task<bool> RollbackAsync(WorkflowExecutionContext context)
    {
        return Task.FromResult(true); // Default: safe to rollback
    }
}

/// <summary>
/// Result of a stage execution.
/// </summary>
public class StageExecutionResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public Dictionary<string, object> Output { get; set; } = new();
    public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Execution context shared across all stages in a workflow.
/// Preserves cross-stage decisions, intermediate outputs, and audit trail.
/// </summary>
public class WorkflowExecutionContext
{
    public WorkflowExecution Execution { get; set; } = new();
    public Dictionary<string, object> SharedContext { get; set; } = new();
    public List<string> ExecutionPath { get; set; } = new(); // Track execution order
    public List<(DateTime, string)> AuditTrail { get; set; } = new(); // Audit-grade observability

    public void LogAudit(string message)
    {
        AuditTrail.Add((DateTime.UtcNow, message));
        Execution.AuditLog ??= new Dictionary<string, string>();
        Execution.AuditLog[$"audit_{DateTime.UtcNow:O}"] = message;
    }
}
