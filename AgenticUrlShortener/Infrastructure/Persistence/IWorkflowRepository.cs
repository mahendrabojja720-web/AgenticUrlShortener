namespace AgenticUrlShortener.Infrastructure.Persistence;

using AgenticUrlShortener.Domain.Models;
using System.Collections.Concurrent;

/// <summary>
/// In-memory repository for workflow execution tracking.
/// </summary>
public interface IWorkflowRepository
{
    Task<WorkflowExecution> CreateAsync(WorkflowExecution execution);
    Task<WorkflowExecution?> GetByIdAsync(string id);
    Task UpdateAsync(WorkflowExecution execution);
    Task<List<WorkflowExecution>> GetByWorkflowTypeAsync(string workflowType);
    Task<List<WorkflowExecution>> GetPendingApprovalsAsync();
}

public class InMemoryWorkflowRepository : IWorkflowRepository
{
    private readonly ConcurrentDictionary<string, WorkflowExecution> _workflows = new();
    private readonly object _lock = new object();

    public Task<WorkflowExecution> CreateAsync(WorkflowExecution execution)
    {
        lock (_lock)
        {
            execution.Id = Guid.NewGuid().ToString();
            execution.AuditLog = execution.AuditLog ?? new Dictionary<string, string>();
            execution.AuditLog[$"created_at_{DateTime.UtcNow:O}"] = $"Workflow created: {execution.WorkflowType}";
            _workflows.TryAdd(execution.Id, execution);
            return Task.FromResult(execution);
        }
    }

    public Task<WorkflowExecution?> GetByIdAsync(string id)
    {
        _workflows.TryGetValue(id, out var execution);
        return Task.FromResult(execution);
    }

    public Task UpdateAsync(WorkflowExecution execution)
    {
        lock (_lock)
        {
            if (_workflows.TryGetValue(execution.Id, out _))
            {
                _workflows[execution.Id] = execution;
            }
        }
        return Task.CompletedTask;
    }

    public Task<List<WorkflowExecution>> GetByWorkflowTypeAsync(string workflowType)
    {
        var results = _workflows.Values
            .Where(w => w.WorkflowType == workflowType)
            .ToList();
        return Task.FromResult(results);
    }

    public Task<List<WorkflowExecution>> GetPendingApprovalsAsync()
    {
        var results = _workflows.Values
            .Where(w => w.Status == WorkflowStatus.AwaitingApproval)
            .ToList();
        return Task.FromResult(results);
    }
}
