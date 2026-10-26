namespace AgenticUrlShortener.Tests.Orchestration;

using Xunit;
using AgenticUrlShortener.Orchestration.Core;
using AgenticUrlShortener.Infrastructure.Persistence;
using AgenticUrlShortener.Orchestration.Stages;
using AgenticUrlShortener.Application.DTOs;
using AgenticUrlShortener.Domain.Models;

public class WorkflowOrchestratorTests
{
    private readonly IWorkflowOrchestrator _orchestrator;
    private readonly IUrlRepository _urlRepository;
    private readonly IWorkflowRepository _workflowRepository;

    public WorkflowOrchestratorTests()
    {
        _urlRepository = new InMemoryUrlRepository();
        _workflowRepository = new InMemoryWorkflowRepository();
        _orchestrator = new WorkflowOrchestrator(_workflowRepository);

        // Register stages
        _orchestrator.RegisterStage(new ValidateAndNormalizeUrlStage());
        _orchestrator.RegisterStage(new CheckDuplicateAndReserveStage(_urlRepository));
        _orchestrator.RegisterStage(new PersistUrlStage(_urlRepository));
        _orchestrator.RegisterStage(new GenerateResponseStage());
    }

    [Fact]
    public async Task ExecuteWorkflow_WithValidDefinition_CompletesSuccessfully()
    {
        // Arrange
        var request = new CreateShortUrlRequest
        {
            OriginalUrl = "https://www.example.com",
            CreatedBy = "test-user"
        };

        var definition = new WorkflowDefinition
        {
            Name = "UrlCreation",
            Stages = new List<string>
            {
                "ValidateAndNormalize",
                "CheckDuplicateAndReserve",
                "PersistUrl",
                "GenerateResponse"
            },
            Dependencies = new Dictionary<string, List<string>>
            {
                { "CheckDuplicateAndReserve", new List<string> { "ValidateAndNormalize" } },
                { "PersistUrl", new List<string> { "CheckDuplicateAndReserve" } },
                { "GenerateResponse", new List<string> { "PersistUrl" } }
            },
            InitialContext = new Dictionary<string, object>
            {
                { "request", request }
            }
        };

        // Act
        var execution = await _orchestrator.ExecuteAsync(definition, "test-user");

        // Assert
        Assert.NotNull(execution);
        Assert.Equal(WorkflowStatus.Completed, execution.Status);
        Assert.Equal(4, execution.Stages.Count);
        Assert.All(execution.Stages, s => Assert.Equal(StageStatus.Completed, s.Status));
    }

    [Fact]
    public async Task ExecuteWorkflow_WithApprovalRequired_AwaitsPendingApproval()
    {
        // Arrange
        var request = new CreateShortUrlRequest
        {
            OriginalUrl = "https://www.example.com"
        };

        var definition = new WorkflowDefinition
        {
            Name = "UrlDeletion",
            RequiresApproval = true,
            ApprovalCheckpoints = new List<string> { "admin" },
            InitialContext = new Dictionary<string, object>
            {
                { "request", request }
            }
        };

        // Act
        var execution = await _orchestrator.ExecuteAsync(definition, "test-user");

        // Assert
        Assert.Equal(WorkflowStatus.AwaitingApproval, execution.Status);
    }

    [Fact]
    public async Task ApproveWorkflow_WithPendingApproval_ExecutesAndCompletes()
    {
        // Arrange
        var request = new CreateShortUrlRequest
        {
            OriginalUrl = "https://www.example.com"
        };

        var definition = new WorkflowDefinition
        {
            Name = "UrlCreation",
            Stages = new List<string>
            {
                "ValidateAndNormalize",
                "CheckDuplicateAndReserve",
                "PersistUrl",
                "GenerateResponse"
            },
            Dependencies = new Dictionary<string, List<string>>
            {
                { "CheckDuplicateAndReserve", new List<string> { "ValidateAndNormalize" } },
                { "PersistUrl", new List<string> { "CheckDuplicateAndReserve" } },
                { "GenerateResponse", new List<string> { "PersistUrl" } }
            },
            RequiresApproval = true,
            InitialContext = new Dictionary<string, object>
            {
                { "request", request }
            }
        };

        var execution = await _orchestrator.ExecuteAsync(definition, "test-user");
        var workflowId = execution.Id;

        // Act
        var approved = await _orchestrator.ApproveWorkflowAsync(workflowId, "approver", "Looks good");

        // Assert
        Assert.True(approved);
        var updatedExecution = await _orchestrator.GetExecutionStatusAsync(workflowId);
        Assert.NotNull(updatedExecution);
        Assert.Equal(WorkflowStatus.Completed, updatedExecution.Status);
        Assert.Equal("approver", updatedExecution.ApprovedBy);
    }

    [Fact]
    public async Task RejectWorkflow_WithPendingApproval_CancelsWorkflow()
    {
        // Arrange
        var definition = new WorkflowDefinition
        {
            Name = "UrlCreation",
            RequiresApproval = true
        };

        var execution = await _orchestrator.ExecuteAsync(definition, "test-user");
        var workflowId = execution.Id;

        // Act
        var rejected = await _orchestrator.RejectWorkflowAsync(workflowId, "reviewer", "Needs changes");

        // Assert
        Assert.True(rejected);
        var updatedExecution = await _orchestrator.GetExecutionStatusAsync(workflowId);
        Assert.Equal(WorkflowStatus.Cancelled, updatedExecution.Status);
        Assert.Equal("Needs changes", updatedExecution.RejectionReason);
    }

    [Fact]
    public async Task GetPendingApprovals_ReturnsPendingWorkflows()
    {
        // Arrange
        var definition1 = new WorkflowDefinition
        {
            Name = "UrlCreation",
            RequiresApproval = true
        };
        var definition2 = new WorkflowDefinition
        {
            Name = "UrlCreation",
            RequiresApproval = true
        };

        await _orchestrator.ExecuteAsync(definition1, "user1");
        await _orchestrator.ExecuteAsync(definition2, "user2");

        // Act
        var pending = await _orchestrator.GetPendingApprovalsAsync();

        // Assert
        Assert.Equal(2, pending.Count);
    }

    [Fact]
    public async Task GetExecutionStatus_WithValidWorkflowId_ReturnsExecution()
    {
        // Arrange
        var definition = new WorkflowDefinition
        {
            Name = "UrlCreation"
        };

        var execution = await _orchestrator.ExecuteAsync(definition, "test-user");
        var workflowId = execution.Id;

        // Act
        var retrieved = await _orchestrator.GetExecutionStatusAsync(workflowId);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(workflowId, retrieved.Id);
    }

    [Fact]
    public async Task RollbackWorkflow_WithCompletedStages_RollsBackInReverseOrder()
    {
        // Arrange
        var request = new CreateShortUrlRequest
        {
            OriginalUrl = "https://www.example.com"
        };

        var definition = new WorkflowDefinition
        {
            Name = "UrlCreation",
            Stages = new List<string>
            {
                "ValidateAndNormalize",
                "CheckDuplicateAndReserve",
                "PersistUrl",
                "GenerateResponse"
            },
            Dependencies = new Dictionary<string, List<string>>
            {
                { "CheckDuplicateAndReserve", new List<string> { "ValidateAndNormalize" } },
                { "PersistUrl", new List<string> { "CheckDuplicateAndReserve" } },
                { "GenerateResponse", new List<string> { "PersistUrl" } }
            },
            InitialContext = new Dictionary<string, object>
            {
                { "request", request }
            }
        };

        var execution = await _orchestrator.ExecuteAsync(definition, "test-user");
        var workflowId = execution.Id;

        // Act
        var success = await _orchestrator.RollbackWorkflowAsync(workflowId, "Testing rollback");

        // Assert
        Assert.True(success);
        var updatedExecution = await _orchestrator.GetExecutionStatusAsync(workflowId);
        Assert.Equal(WorkflowStatus.RolledBack, updatedExecution.Status);
    }

    [Fact]
    public async Task Workflow_PreservesAuditTrail()
    {
        // Arrange
        var request = new CreateShortUrlRequest
        {
            OriginalUrl = "https://www.example.com"
        };

        var definition = new WorkflowDefinition
        {
            Name = "UrlCreation",
            Stages = new List<string>
            {
                "ValidateAndNormalize",
                "CheckDuplicateAndReserve",
                "PersistUrl",
                "GenerateResponse"
            },
            Dependencies = new Dictionary<string, List<string>>
            {
                { "CheckDuplicateAndReserve", new List<string> { "ValidateAndNormalize" } },
                { "PersistUrl", new List<string> { "CheckDuplicateAndReserve" } },
                { "GenerateResponse", new List<string> { "PersistUrl" } }
            },
            InitialContext = new Dictionary<string, object>
            {
                { "request", request }
            }
        };

        // Act
        var execution = await _orchestrator.ExecuteAsync(definition, "test-user");

        // Assert
        Assert.NotNull(execution.AuditLog);
        Assert.NotEmpty(execution.AuditLog);
        Assert.True(execution.AuditLog.Any(kv => kv.Value.Contains("Workflow started")));
    }
}
