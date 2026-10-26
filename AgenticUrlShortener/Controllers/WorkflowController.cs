namespace AgenticUrlShortener.Controllers;

using Microsoft.AspNetCore.Mvc;
using AgenticUrlShortener.Orchestration.Core;
using AgenticUrlShortener.Domain.Models;

[ApiController]
[Route("api/[controller]")]
public class WorkflowController : ControllerBase
{
    private readonly IWorkflowOrchestrator _orchestrator;
    private readonly ILogger<WorkflowController> _logger;

    public WorkflowController(IWorkflowOrchestrator orchestrator, ILogger<WorkflowController> logger)
    {
        _orchestrator = orchestrator;
        _logger = logger;
    }

    /// <summary>
    /// Get the status of a workflow execution.
    /// </summary>
    [HttpGet("{workflowId}")]
    public async Task<IActionResult> GetWorkflowStatus(string workflowId)
    {
        try
        {
            var execution = await _orchestrator.GetExecutionStatusAsync(workflowId);
            if (execution == null)
            {
                return NotFound(new { error = "Workflow not found" });
            }
            return Ok(execution);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving workflow status");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Get all workflows pending approval.
    /// </summary>
    [HttpGet("approvals/pending")]
    public async Task<IActionResult> GetPendingApprovals()
    {
        try
        {
            var workflows = await _orchestrator.GetPendingApprovalsAsync();
            return Ok(workflows);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving pending approvals");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Approve a workflow.
    /// </summary>
    [HttpPost("{workflowId}/approve")]
    public async Task<IActionResult> ApproveWorkflow(string workflowId, [FromBody] ApprovalRequest request)
    {
        try
        {
            var success = await _orchestrator.ApproveWorkflowAsync(workflowId, request.ApprovedBy, request.Reason);
            if (!success)
            {
                return BadRequest(new { error = "Cannot approve workflow. Check status." });
            }
            return Ok(new { message = "Workflow approved and execution started" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving workflow");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Reject a workflow.
    /// </summary>
    [HttpPost("{workflowId}/reject")]
    public async Task<IActionResult> RejectWorkflow(string workflowId, [FromBody] ApprovalRequest request)
    {
        try
        {
            var success = await _orchestrator.RejectWorkflowAsync(workflowId, request.ApprovedBy, request.Reason);
            if (!success)
            {
                return BadRequest(new { error = "Cannot reject workflow. Check status." });
            }
            return Ok(new { message = "Workflow rejected" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting workflow");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Rollback a workflow.
    /// </summary>
    [HttpPost("{workflowId}/rollback")]
    public async Task<IActionResult> RollbackWorkflow(string workflowId, [FromBody] RollbackRequest request)
    {
        try
        {
            var success = await _orchestrator.RollbackWorkflowAsync(workflowId, request.Reason);
            if (!success)
            {
                return BadRequest(new { error = "Cannot rollback workflow. Check status." });
            }
            return Ok(new { message = "Workflow rolled back" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rolling back workflow");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }
}

public class ApprovalRequest
{
    public string ApprovedBy { get; set; } = string.Empty;
    public string? Reason { get; set; }
}

public class RollbackRequest
{
    public string Reason { get; set; } = string.Empty;
}
