namespace AgenticUrlShortener.Orchestration.Core;

using AgenticUrlShortener.Domain.Models;
using System.Collections.Concurrent;

/// <summary>
/// Workflow orchestration engine with dependency graph resolution,
/// synchronization, retries, rollbacks, and governance controls.
/// </summary>
public interface IWorkflowOrchestrator
{
    Task<WorkflowExecution> ExecuteAsync(WorkflowDefinition definition, string userId);
    Task<bool> ApproveWorkflowAsync(string workflowId, string approvedBy, string? reason = null);
    Task<bool> RejectWorkflowAsync(string workflowId, string rejectedBy, string reason);
    Task<bool> RollbackWorkflowAsync(string workflowId, string reason);
    Task<WorkflowExecution?> GetExecutionStatusAsync(string workflowId);
    Task<List<WorkflowExecution>> GetPendingApprovalsAsync();
}

public class WorkflowOrchestrator : IWorkflowOrchestrator
{
    private readonly Infrastructure.Persistence.IWorkflowRepository _repository;
    private readonly Dictionary<string, WorkflowStage> _stageRegistry;
    private const int MaxRetries = 3;
    private const int RetryDelayMs = 1000;

    public WorkflowOrchestrator(Infrastructure.Persistence.IWorkflowRepository repository)
    {
        _repository = repository;
        _stageRegistry = new Dictionary<string, WorkflowStage>();
    }

    public void RegisterStage(WorkflowStage stage)
    {
        _stageRegistry[stage.StageName] = stage;
    }

    public async Task<WorkflowExecution> ExecuteAsync(WorkflowDefinition definition, string userId)
    {
        var execution = new WorkflowExecution
        {
            WorkflowType = definition.Name,
            Status = WorkflowStatus.Pending,
            Context = definition.InitialContext,
            Stages = definition.Stages.Select(s => new StageExecution
            {
                StageName = s,
                Status = StageStatus.Pending,
                DependsOn = GetStageDependencies(s, definition)
            }).ToList()
        };

        execution = await _repository.CreateAsync(execution);
        var context = new WorkflowExecutionContext { Execution = execution };
        context.LogAudit($"Workflow started by {userId}");

        // Check if approval is required
        if (definition.RequiresApproval)
        {
            execution.Status = WorkflowStatus.AwaitingApproval;
            execution.ApprovalCheckpoints = definition.ApprovalCheckpoints;
            await _repository.UpdateAsync(execution);
            context.LogAudit("Workflow awaiting approval");
            return execution;
        }

        // Execute workflow stages
        return await ExecuteStagesAsync(execution, definition, context);
    }

    public async Task<bool> ApproveWorkflowAsync(string workflowId, string approvedBy, string? reason = null)
    {
        var execution = await _repository.GetByIdAsync(workflowId);
        if (execution == null || execution.Status != WorkflowStatus.AwaitingApproval)
        {
            return false;
        }

        execution.Status = WorkflowStatus.Approved;
        execution.ApprovedBy = approvedBy;
        execution.ApprovedAt = DateTime.UtcNow;
        await _repository.UpdateAsync(execution);

        var context = new WorkflowExecutionContext { Execution = execution };
        context.LogAudit($"Workflow approved by {approvedBy}. Reason: {reason}");

        // Get workflow definition and execute
        var definition = new WorkflowDefinition { Name = execution.WorkflowType };
        await ExecuteStagesAsync(execution, definition, context);

        return true;
    }

    public async Task<bool> RejectWorkflowAsync(string workflowId, string rejectedBy, string reason)
    {
        var execution = await _repository.GetByIdAsync(workflowId);
        if (execution == null || execution.Status != WorkflowStatus.AwaitingApproval)
        {
            return false;
        }

        execution.Status = WorkflowStatus.Cancelled;
        execution.RejectionReason = reason;
        await _repository.UpdateAsync(execution);
        return true;
    }

    public async Task<bool> RollbackWorkflowAsync(string workflowId, string reason)
    {
        var execution = await _repository.GetByIdAsync(workflowId);
        if (execution == null)
        {
            return false;
        }

        execution.Status = WorkflowStatus.RolledBack;
        var context = new WorkflowExecutionContext { Execution = execution };
        context.LogAudit($"Rollback initiated: {reason}");

        // Rollback completed stages in reverse order
        var completedStages = execution.Stages
            .Where(s => s.Status == StageStatus.Completed)
            .OrderByDescending(s => s.CompletedAt)
            .ToList();

        foreach (var stage in completedStages)
        {
            if (_stageRegistry.TryGetValue(stage.StageName, out var stageHandler))
            {
                await stageHandler.RollbackAsync(context);
                stage.Status = StageStatus.RolledBack;
                context.LogAudit($"Stage rolled back: {stage.StageName}");
            }
        }

        await _repository.UpdateAsync(execution);
        return true;
    }

    public async Task<WorkflowExecution?> GetExecutionStatusAsync(string workflowId)
    {
        return await _repository.GetByIdAsync(workflowId);
    }

    public async Task<List<WorkflowExecution>> GetPendingApprovalsAsync()
    {
        return await _repository.GetPendingApprovalsAsync();
    }

    private async Task<WorkflowExecution> ExecuteStagesAsync(
        WorkflowExecution execution,
        WorkflowDefinition definition,
        WorkflowExecutionContext context)
    {
        execution.Status = WorkflowStatus.Executing;
        await _repository.UpdateAsync(execution);

        var completed = new HashSet<string>();
        var failed = new List<string>();

        while (completed.Count < execution.Stages.Count && failed.Count == 0)
        {
            var readyStages = execution.Stages
                .Where(s => s.Status == StageStatus.Pending &&
                           s.DependsOn.All(d => completed.Contains(d)))
                .ToList();

            if (readyStages.Count == 0 && completed.Count < execution.Stages.Count)
            {
                break; // Deadlock or circular dependency
            }

            // Execute ready stages (parallel where no conflicts)
            var stageTasks = readyStages.Select(s => ExecuteSingleStageAsync(s, context)).ToList();
            await Task.WhenAll(stageTasks);

            // Check for failures and update completed set
            foreach (var stage in readyStages)
            {
                if (stage.Status == StageStatus.Completed)
                {
                    completed.Add(stage.StageName);
                    context.ExecutionPath.Add(stage.StageName);
                }
                else if (stage.Status == StageStatus.Failed)
                {
                    failed.Add(stage.StageName);
                }
            }

            await _repository.UpdateAsync(execution);
        }

        // Finalize execution
        execution.Status = failed.Count > 0 ? WorkflowStatus.Failed : WorkflowStatus.Completed;
        execution.CompletedAt = DateTime.UtcNow;
        await _repository.UpdateAsync(execution);

        return execution;
    }

    private async Task ExecuteSingleStageAsync(StageExecution stageExec, WorkflowExecutionContext context)
    {
        if (!_stageRegistry.TryGetValue(stageExec.StageName, out var stageHandler))
        {
            stageExec.Status = StageStatus.Failed;
            stageExec.ErrorMessage = $"Stage handler not found: {stageExec.StageName}";
            return;
        }

        // Validate entry gates
        var (valid, reason) = await stageHandler.ValidateEntryGatesAsync(context);
        if (!valid)
        {
            stageExec.Status = StageStatus.Failed;
            stageExec.ErrorMessage = reason;
            context.LogAudit($"Stage entry gate failed: {stageExec.StageName} - {reason}");
            return;
        }

        stageExec.StartedAt = DateTime.UtcNow;
        stageExec.Status = StageStatus.Running;
        context.LogAudit($"Stage started: {stageExec.StageName}");

        // Execute with retries
        for (int attempt = 0; attempt <= MaxRetries; attempt++)
        {
            try
            {
                var result = await stageHandler.ExecuteAsync(context);

                if (result.Success)
                {
                    stageExec.Status = StageStatus.Completed;
                    stageExec.Output = result.Output;
                    stageExec.CompletedAt = DateTime.UtcNow;
                    context.LogAudit($"Stage completed: {stageExec.StageName}");
                    return;
                }
                else
                {
                    stageExec.ErrorMessage = result.ErrorMessage;
                    if (attempt < MaxRetries)
                    {
                        stageExec.Status = StageStatus.Retrying;
                        stageExec.RetryCount++;
                        context.LogAudit($"Stage retry {stageExec.RetryCount}/{MaxRetries}: {stageExec.StageName}");
                        await Task.Delay(RetryDelayMs);
                    }
                }
            }
            catch (Exception ex)
            {
                stageExec.ErrorMessage = ex.Message;
                if (attempt < MaxRetries)
                {
                    stageExec.Status = StageStatus.Retrying;
                    stageExec.RetryCount++;
                    context.LogAudit($"Stage exception retry {stageExec.RetryCount}/{MaxRetries}: {ex.Message}");
                    await Task.Delay(RetryDelayMs);
                }
            }
        }

        stageExec.Status = StageStatus.Failed;
        context.LogAudit($"Stage failed after {MaxRetries} retries: {stageExec.StageName}");
    }

    private static List<string> GetStageDependencies(string stageName, WorkflowDefinition definition)
    {
        return definition.Dependencies.ContainsKey(stageName)
            ? definition.Dependencies[stageName]
            : new List<string>();
    }
}

/// <summary>
/// Workflow definition with stages, dependencies, and governance policies.
/// </summary>
public class WorkflowDefinition
{
    public string Name { get; set; } = string.Empty;
    public List<string> Stages { get; set; } = new();
    public Dictionary<string, List<string>> Dependencies { get; set; } = new();
    public bool RequiresApproval { get; set; }
    public List<string> ApprovalCheckpoints { get; set; } = new();
    public Dictionary<string, object> InitialContext { get; set; } = new();
}
