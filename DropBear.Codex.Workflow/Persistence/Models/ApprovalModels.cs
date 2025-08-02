namespace DropBear.Codex.Workflow.Persistence.Models;

/// <summary>
/// Represents an approval request
/// </summary>
public sealed record ApprovalRequest
{
    public required string RequestId { get; init; }
    public required string WorkflowInstanceId { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required string RequestedBy { get; init; }
    public required DateTimeOffset RequestedAt { get; init; }
    public required List<string> ApproverEmails { get; init; }
    public TimeSpan? Timeout { get; init; }
    public Dictionary<string, object> Context { get; init; } = new();
    public string? CallbackUrl { get; init; }
}

/// <summary>
/// Represents an approval response
/// </summary>
public sealed record ApprovalResponse
{
    public required string RequestId { get; init; }
    public required bool IsApproved { get; init; }
    public required string ApprovedBy { get; init; }
    public required DateTimeOffset ApprovedAt { get; init; }
    public string? Comments { get; init; }
    public Dictionary<string, object> Metadata { get; init; } = new();
}

/// <summary>
/// Result of validation operations
/// </summary>
public sealed record ValidationResult
{
    public required bool IsValid { get; init; }
    public string? ErrorMessage { get; init; }
    public List<string> ValidationErrors { get; init; } = new();
    public Dictionary<string, object> Metadata { get; init; } = new();

    public static ValidationResult Success() => new() { IsValid = true };
    public static ValidationResult Failure(string errorMessage, List<string>? validationErrors = null) => 
        new() { IsValid = false, ErrorMessage = errorMessage, ValidationErrors = validationErrors ?? new() };
}
