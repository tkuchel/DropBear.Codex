#region

using DropBear.Codex.Workflow.Metrics;
using DropBear.Codex.Workflow.Persistence.Interfaces;
using DropBear.Codex.Workflow.Persistence.Models;

#endregion

namespace DropBear.Codex.Workflow.Examples;

/// <summary>
///     Usage examples for persistent workflows
/// </summary>
public static class PersistentWorkflowUsageExamples
{
    /// <summary>
    ///     Example: Starting a document approval workflow
    /// </summary>
    public static async Task<string> StartDocumentApprovalWorkflow(
        IPersistentWorkflowEngine workflowEngine,
        string documentId,
        string authorId,
        List<string> approverIds)
    {
        var workflow = new DocumentApprovalWorkflow();
        var context = new DocumentApprovalContext
        {
            DocumentId = documentId,
            DocumentTitle = "Important Document",
            AuthorId = authorId,
            ApproverIds = approverIds
        };

        PersistentWorkflowResult<DocumentApprovalContext> result =
            await workflowEngine.StartPersistentWorkflowAsync(workflow, context);

        Console.WriteLine($"Started workflow {result.WorkflowInstanceId}");
        Console.WriteLine($"Status: {result.Status}");

        if (result.IsWaiting)
        {
            Console.WriteLine($"Waiting for: {result.WaitingForSignal}");
            if (result.SignalTimeoutAt.HasValue)
            {
                Console.WriteLine($"Timeout at: {result.SignalTimeoutAt}");
            }
        }

        return result.WorkflowInstanceId;
    }

    /// <summary>
    ///     Example: Approving a document
    /// </summary>
    public static async Task<bool> ApproveDocument(
        IPersistentWorkflowEngine workflowEngine,
        string workflowInstanceId,
        string approverId,
        bool isApproved,
        string? comments = null)
    {
        var approvalResponse = new ApprovalResponse
        {
            RequestId = Guid.NewGuid().ToString(),
            IsApproved = isApproved,
            ApprovedBy = approverId,
            ApprovedAt = DateTimeOffset.UtcNow,
            Comments = comments
        };

        // The signal name would need to match what the workflow is waiting for
        string signalName = $"approval_RequestDocumentApprovalStep_{workflowInstanceId.GetHashCode()}";

        return await workflowEngine.SignalWorkflowAsync(workflowInstanceId, signalName, approvalResponse);
    }

    /// <summary>
    ///     Example: Starting a user profile update workflow
    /// </summary>
    public static async Task<string> StartUserProfileUpdateWorkflow(
        IPersistentWorkflowEngine workflowEngine,
        string userId,
        string requestedByUserId)
    {
        var workflow = new UserProfileUpdateWorkflow();
        var context = new UserProfileUpdateContext { UserId = userId, RequestedByUserId = requestedByUserId };

        PersistentWorkflowResult<UserProfileUpdateContext> result =
            await workflowEngine.StartPersistentWorkflowAsync(workflow, context);

        Console.WriteLine($"Started profile update workflow {result.WorkflowInstanceId}");
        Console.WriteLine($"Status: {result.Status}");
        Console.WriteLine("User can now modify their profile data");

        return result.WorkflowInstanceId;
    }

    /// <summary>
    ///     Example: User submits profile modifications
    /// </summary>
    public static async Task<bool> SubmitProfileModifications(
        IPersistentWorkflowEngine workflowEngine,
        string workflowInstanceId,
        ProfileModificationData modifications)
    {
        string signalName = $"user_modification_UserProfileModificationStep_{workflowInstanceId.GetHashCode()}";

        return await workflowEngine.SignalWorkflowAsync(workflowInstanceId, signalName, modifications);
    }

    /// <summary>
    ///     Example: Monitoring workflow progress
    /// </summary>
    public static async Task MonitorWorkflowProgress<TContext>(
        IPersistentWorkflowEngine workflowEngine,
        string workflowInstanceId) where TContext : class
    {
        WorkflowInstanceState<TContext>? state =
            await workflowEngine.GetWorkflowStateAsync<TContext>(workflowInstanceId);

        if (state == null)
        {
            Console.WriteLine("Workflow not found");
            return;
        }

        Console.WriteLine($"Workflow {workflowInstanceId}:");
        Console.WriteLine($"  Status: {state.Status}");
        Console.WriteLine($"  Created: {state.CreatedAt}");
        Console.WriteLine($"  Last Updated: {state.LastUpdatedAt}");

        if (!string.IsNullOrEmpty(state.WaitingForSignal))
        {
            Console.WriteLine($"  Waiting for: {state.WaitingForSignal}");
        }

        if (state.SignalTimeoutAt.HasValue)
        {
            Console.WriteLine($"  Timeout: {state.SignalTimeoutAt}");
        }

        Console.WriteLine($"  Execution History: {state.ExecutionHistory.Count} steps");
        foreach (StepExecutionTrace checkpoint in state.ExecutionHistory)
        {
            string status = checkpoint.Succeeded ? "✅" : "❌";
            Console.WriteLine($"    {status} {checkpoint.StepName} at {checkpoint.EndTime}");
        }
    }

    /// <summary>
    ///     Example: Complete document approval flow
    /// </summary>
    public static async Task RunCompleteDocumentApprovalExample(IPersistentWorkflowEngine workflowEngine)
    {
        Console.WriteLine("🚀 Starting Complete Document Approval Example");
        Console.WriteLine("=" + new string('=', 50));

        try
        {
            // Step 1: Start the workflow
            string workflowId = await StartDocumentApprovalWorkflow(
                workflowEngine,
                "DOC-12345",
                "john.author",
                new List<string> { "jane.manager", "bob.director" }
            );

            Console.WriteLine($"\n📝 Document workflow started: {workflowId}");

            // Step 2: Monitor initial state
            await MonitorWorkflowProgress<DocumentApprovalContext>(workflowEngine, workflowId);

            // Step 3: Simulate approval after delay
            Console.WriteLine("\n⏱️ Simulating approval process (waiting 2 seconds)...");
            await Task.Delay(2000);

            // Step 4: Approve the document
            bool approved = await ApproveDocument(
                workflowEngine,
                workflowId,
                "jane.manager",
                true,
                "Document looks good, approved!"
            );

            Console.WriteLine($"\n✅ Approval submitted: {approved}");

            // Step 5: Wait for workflow to complete
            await Task.Delay(1000);

            // Step 6: Check final state
            Console.WriteLine("\n📊 Final workflow state:");
            await MonitorWorkflowProgress<DocumentApprovalContext>(workflowEngine, workflowId);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error in document approval example: {ex.Message}");
        }
    }

    /// <summary>
    ///     Example: Complete user profile update flow
    /// </summary>
    public static async Task RunCompleteUserProfileUpdateExample(IPersistentWorkflowEngine workflowEngine)
    {
        Console.WriteLine("🚀 Starting Complete User Profile Update Example");
        Console.WriteLine("=" + new string('=', 50));

        try
        {
            // Step 1: Start the workflow
            string workflowId = await StartUserProfileUpdateWorkflow(
                workflowEngine,
                "user123",
                "user123"
            );

            Console.WriteLine($"\n👤 Profile update workflow started: {workflowId}");

            // Step 2: Simulate user modifications
            Console.WriteLine("\n📝 Simulating user profile modifications...");
            await Task.Delay(1000);

            var modifications = new ProfileModificationData
            {
                FirstName = "John",
                LastName = "Updated",
                Email = "john.updated@company.com",
                Department = "Engineering",
                JobTitle = "Senior Developer"
            };

            bool modificationsSubmitted = await SubmitProfileModifications(
                workflowEngine,
                workflowId,
                modifications
            );

            Console.WriteLine($"✅ Profile modifications submitted: {modificationsSubmitted}");

            // Step 3: Monitor state after modifications
            await Task.Delay(1000);
            Console.WriteLine("\n📊 State after modifications:");
            await MonitorWorkflowProgress<UserProfileUpdateContext>(workflowEngine, workflowId);

            // Step 4: Simulate HR approval
            Console.WriteLine("\n👩‍💼 Simulating HR approval...");
            await Task.Delay(1000);

            var approvalResponse = new ApprovalResponse
            {
                RequestId = Guid.NewGuid().ToString(),
                IsApproved = true,
                ApprovedBy = "hr.manager",
                ApprovedAt = DateTimeOffset.UtcNow,
                Comments = "Profile changes approved"
            };

            string approvalSignalName = $"approval_ProfileChangeApprovalStep_{workflowId.GetHashCode()}";
            bool approvalSubmitted = await workflowEngine.SignalWorkflowAsync(
                workflowId,
                approvalSignalName,
                approvalResponse
            );

            Console.WriteLine($"✅ Approval submitted: {approvalSubmitted}");

            // Step 5: Wait for final completion
            await Task.Delay(1000);

            // Step 6: Check final state
            Console.WriteLine("\n📊 Final workflow state:");
            await MonitorWorkflowProgress<UserProfileUpdateContext>(workflowEngine, workflowId);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error in user profile update example: {ex.Message}");
        }
    }
}
