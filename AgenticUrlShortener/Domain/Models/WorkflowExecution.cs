namespace AgenticUrlShortener.Domain.Models;

/// <summary>
/// Represents workflow orchestration state and execution metadata.
/// </summary>
public class WorkflowExecution
{
    public string Id { get; set; } = string.Empty;
    public string WorkflowType { get; set; } = string.Empty;
    public WorkflowStatus Status { get; set; } = WorkflowStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public Dictionary<string, object> Context { get; set; } = new();
    public List<StageExecution> Stages { get; set; } = new();
    public List<string> ApprovalCheckpoints { get; set; } = new();
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? RejectionReason { get; set; }
    public Dictionary<string, string>? AuditLog { get; set; } = new();
}

public enum WorkflowStatus
{
    Pending,
    InProgress,
    AwaitingApproval,
    Approved,
    Executing,
    Completed,
    Failed,
    RolledBack,
    Cancelled
}

public class StageExecution
{
    public string StageName { get; set; } = string.Empty;
    public StageStatus Status { get; set; } = StageStatus.Pending;
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public Dictionary<string, object> Output { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; }
    public List<string> DependsOn { get; set; } = new();
}

public enum StageStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Retrying,
    Skipped,
    RolledBack
}
