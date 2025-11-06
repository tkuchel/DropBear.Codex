#region

using DropBear.Codex.Workflow.Builder;
using DropBear.Codex.Workflow.Core;
using DropBear.Codex.Workflow.Persistence.Models;
using DropBear.Codex.Workflow.Persistence.Steps;
using DropBear.Codex.Workflow.Results;

#endregion

namespace DropBear.Codex.Workflow.Examples;

/// <summary>
///     Context for document approval workflow
/// </summary>
public class DocumentApprovalContext
{
    public required string DocumentId { get; init; }
    public required string DocumentTitle { get; init; }
    public required string AuthorId { get; init; }
    public required List<string> ApproverIds { get; init; }
    public string? ApprovedByUserId { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public bool IsApproved { get; set; }
    public string? ApprovalComments { get; set; }
    public DocumentStatus Status { get; set; } = DocumentStatus.Draft;
}

public enum DocumentStatus
{
    Draft,
    PendingApproval,
    Approved,
    Rejected,
    Published
}

/// <summary>
///     Step that requests document approval
/// </summary>
public class RequestDocumentApprovalStep : WaitForApprovalStep<DocumentApprovalContext>
{
    public override ApprovalRequest CreateApprovalRequest(DocumentApprovalContext context)
    {
        return new ApprovalRequest
        {
            RequestId = Guid.NewGuid().ToString(),
            WorkflowInstanceId = "", // Will be set by the engine
            Title = $"Approval Required: {context.DocumentTitle}",
            Description = $"Please review and approve the document '{context.DocumentTitle}' by {context.AuthorId}",
            RequestedBy = context.AuthorId,
            RequestedAt = DateTimeOffset.UtcNow,
            ApproverEmails = context.ApproverIds.Select(id => $"{id}@company.com").ToList(),
            Timeout = TimeSpan.FromDays(3),
            Context = new Dictionary<string, object>
            {
                ["DocumentId"] = context.DocumentId,
                ["DocumentTitle"] = context.DocumentTitle,
                ["AuthorId"] = context.AuthorId
            }
        };
    }

    protected override ValueTask OnApprovalReceivedAsync(DocumentApprovalContext context,
        ApprovalResponse approvalResponse, CancellationToken cancellationToken)
    {
        context.IsApproved = approvalResponse.IsApproved;
        context.ApprovedByUserId = approvalResponse.ApprovedBy;
        context.ApprovedAt = approvalResponse.ApprovedAt;
        context.ApprovalComments = approvalResponse.Comments;
        context.Status = approvalResponse.IsApproved ? DocumentStatus.Approved : DocumentStatus.Rejected;

        return ValueTask.CompletedTask;
    }
}

/// <summary>
///     Step that publishes the approved document
/// </summary>
public class PublishDocumentStep : WorkflowStepBase<DocumentApprovalContext>
{
    public override async ValueTask<StepResult> ExecuteAsync(DocumentApprovalContext context,
        CancellationToken cancellationToken = default)
    {
        if (!context.IsApproved)
        {
            return Failure("Cannot publish document - not approved");
        }

        // Simulate document publishing
        await Task.Delay(500, cancellationToken);

        context.Status = DocumentStatus.Published;

        var metadata = new Dictionary<string, object>
        {
            ["PublishedAt"] = DateTimeOffset.UtcNow, ["PublishedBy"] = "System"
        };

        return Success(metadata);
    }
}

/// <summary>
///     Document approval workflow definition
/// </summary>
public class DocumentApprovalWorkflow : Workflow<DocumentApprovalContext>
{
    public override string WorkflowId => "document-approval-v1";
    public override string DisplayName => "Document Approval Workflow";
    public override TimeSpan? WorkflowTimeout => TimeSpan.FromDays(7);

    protected override void Configure(WorkflowBuilder<DocumentApprovalContext> builder)
    {
        builder
            .StartWith<RequestDocumentApprovalStep>()
            .If(ctx => ctx.IsApproved)
            .ThenExecute<PublishDocumentStep>()
            .EndIf();
    }
}
