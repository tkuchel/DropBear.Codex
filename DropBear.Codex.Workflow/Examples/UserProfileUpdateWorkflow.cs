#region

using System.Net.Mail;
using DropBear.Codex.Workflow.Builder;
using DropBear.Codex.Workflow.Core;
using DropBear.Codex.Workflow.Persistence.Models;
using DropBear.Codex.Workflow.Persistence.Steps;

#endregion

namespace DropBear.Codex.Workflow.Examples;

/// <summary>
///     Context for user profile update workflow
/// </summary>
public class UserProfileUpdateContext
{
    public required string UserId { get; init; }
    public required string RequestedByUserId { get; init; }
    public Dictionary<string, object> CurrentProfile { get; set; } = new();
    public Dictionary<string, object> ProposedChanges { get; set; } = new();
    public Dictionary<string, object> PendingChanges { get; set; } = new();
    public bool ChangesApproved { get; set; }
    public bool ChangesCommitted { get; set; }
    public string? ModificationRequestId { get; set; }
}

/// <summary>
///     Data model for profile modifications
/// </summary>
public class ProfileModificationData
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Email { get; set; }
    public string? Department { get; set; }
    public string? JobTitle { get; set; }
    public Dictionary<string, object> CustomFields { get; set; } = new();
}

/// <summary>
///     Step that allows user to modify their profile data
/// </summary>
public class UserProfileModificationStep : UserModificationStep<UserProfileUpdateContext, ProfileModificationData>
{
    private readonly IUserProfileRepository _userRepository;

    public UserProfileModificationStep(IUserProfileRepository userRepository)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
    }

    public override async ValueTask<ProfileModificationData> PrepareModificationDataAsync(
        UserProfileUpdateContext context,
        CancellationToken cancellationToken = default)
    {
        // Load current profile data
        Dictionary<string, object>? currentProfile =
            await _userRepository.GetUserProfileAsync(context.UserId, cancellationToken);

        context.CurrentProfile = currentProfile ?? new Dictionary<string, object>();

        // Return current data for modification
        return new ProfileModificationData
        {
            FirstName = currentProfile?.GetValueOrDefault("FirstName")?.ToString(),
            LastName = currentProfile?.GetValueOrDefault("LastName")?.ToString(),
            Email = currentProfile?.GetValueOrDefault("Email")?.ToString(),
            Department = currentProfile?.GetValueOrDefault("Department")?.ToString(),
            JobTitle = currentProfile?.GetValueOrDefault("JobTitle")?.ToString()
        };
    }

    protected override async ValueTask<ValidationResult> ValidateModificationsAsync(
        UserProfileUpdateContext context,
        ProfileModificationData modificationData,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();

        // Validate email format
        if (!string.IsNullOrEmpty(modificationData.Email) && !IsValidEmail(modificationData.Email))
        {
            errors.Add("Invalid email format");
        }

        // Validate required fields
        if (string.IsNullOrWhiteSpace(modificationData.FirstName))
        {
            errors.Add("First name is required");
        }

        if (string.IsNullOrWhiteSpace(modificationData.LastName))
        {
            errors.Add("Last name is required");
        }

        // Check for duplicate email
        if (!string.IsNullOrEmpty(modificationData.Email))
        {
            Dictionary<string, object>? existingUser =
                await _userRepository.FindUserByEmailAsync(modificationData.Email, cancellationToken);
            if (existingUser != null && existingUser.GetValueOrDefault("UserId")?.ToString() != context.UserId)
            {
                errors.Add("Email address is already in use");
            }
        }

        return errors.Any()
            ? ValidationResult.Failure("Validation failed", errors)
            : ValidationResult.Success();
    }

    protected override async ValueTask StorePendingChangesAsync(
        UserProfileUpdateContext context,
        ProfileModificationData modificationData,
        CancellationToken cancellationToken = default)
    {
        // Convert modification data to dictionary format
        var pendingChanges = new Dictionary<string, object>();

        if (!string.IsNullOrEmpty(modificationData.FirstName))
        {
            pendingChanges["FirstName"] = modificationData.FirstName;
        }

        if (!string.IsNullOrEmpty(modificationData.LastName))
        {
            pendingChanges["LastName"] = modificationData.LastName;
        }

        if (!string.IsNullOrEmpty(modificationData.Email))
        {
            pendingChanges["Email"] = modificationData.Email;
        }

        if (!string.IsNullOrEmpty(modificationData.Department))
        {
            pendingChanges["Department"] = modificationData.Department;
        }

        if (!string.IsNullOrEmpty(modificationData.JobTitle))
        {
            pendingChanges["JobTitle"] = modificationData.JobTitle;
        }

        // Store in context and repository
        context.PendingChanges = pendingChanges;
        context.ProposedChanges = pendingChanges;
        context.ModificationRequestId = Guid.NewGuid().ToString();

        await _userRepository.StorePendingChangesAsync(context.UserId, context.ModificationRequestId, pendingChanges,
            cancellationToken);
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            var addr = new MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
///     Step that requests approval for profile changes
/// </summary>
public class ProfileChangeApprovalStep : WaitForApprovalStep<UserProfileUpdateContext>
{
    public override ApprovalRequest CreateApprovalRequest(UserProfileUpdateContext context)
    {
        string changesSummary = string.Join(", ", context.ProposedChanges.Select(kvp => $"{kvp.Key}: {kvp.Value}"));

        return new ApprovalRequest
        {
            RequestId = Guid.NewGuid().ToString(),
            WorkflowInstanceId = "", // Will be set by engine
            Title = "Profile Change Approval Required",
            Description = $"User {context.UserId} has requested the following profile changes: {changesSummary}",
            RequestedBy = context.RequestedByUserId,
            RequestedAt = DateTimeOffset.UtcNow,
            ApproverEmails = new List<string> { "hr@company.com", "manager@company.com" },
            Timeout = TimeSpan.FromDays(2),
            Context = new Dictionary<string, object>
            {
                ["UserId"] = context.UserId,
                ["ProposedChanges"] = context.ProposedChanges,
                ["RequestedBy"] = context.RequestedByUserId
            }
        };
    }

    protected override ValueTask OnApprovalReceivedAsync(UserProfileUpdateContext context,
        ApprovalResponse approvalResponse, CancellationToken cancellationToken)
    {
        context.ChangesApproved = approvalResponse.IsApproved;
        return ValueTask.CompletedTask;
    }
}

/// <summary>
///     Step that commits the approved profile changes
/// </summary>
public class CommitProfileChangesStep : CommitChangesStep<UserProfileUpdateContext>
{
    private readonly IUserProfileRepository _userRepository;

    public CommitProfileChangesStep(IUserProfileRepository userRepository)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
    }

    protected override async ValueTask<Dictionary<string, object>?> GetPendingChangesAsync(
        UserProfileUpdateContext context,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(context.ModificationRequestId))
        {
            return null;
        }

        return await _userRepository.GetPendingChangesAsync(context.UserId, context.ModificationRequestId,
            cancellationToken);
    }

    protected override async ValueTask CommitPendingChangesAsync(
        UserProfileUpdateContext context,
        Dictionary<string, object> pendingChanges,
        CancellationToken cancellationToken = default)
    {
        await _userRepository.UpdateUserProfileAsync(context.UserId, pendingChanges, cancellationToken);
        context.ChangesCommitted = true;
    }

    protected override async ValueTask ClearPendingChangesAsync(
        UserProfileUpdateContext context,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrEmpty(context.ModificationRequestId))
        {
            await _userRepository.ClearPendingChangesAsync(context.UserId, context.ModificationRequestId,
                cancellationToken);
        }
    }

    protected override async ValueTask RollbackCommittedChangesAsync(
        UserProfileUpdateContext context,
        CancellationToken cancellationToken = default)
    {
        // Restore original profile data
        await _userRepository.UpdateUserProfileAsync(context.UserId, context.CurrentProfile, cancellationToken);
    }
}

/// <summary>
///     Complete user profile update workflow
/// </summary>
public class UserProfileUpdateWorkflow : Workflow<UserProfileUpdateContext>
{
    public override string WorkflowId => "user-profile-update-v1";
    public override string DisplayName => "User Profile Update Workflow";
    public override TimeSpan? WorkflowTimeout => TimeSpan.FromDays(5);

    protected override void Configure(WorkflowBuilder<UserProfileUpdateContext> builder)
    {
        builder
            .StartWith<UserProfileModificationStep>()
            .Then<ProfileChangeApprovalStep>()
            .If(ctx => ctx.ChangesApproved)
            .ThenExecute<CommitProfileChangesStep>()
            .EndIf();
    }
}

/// <summary>
///     Repository interface for user profile operations
/// </summary>
public interface IUserProfileRepository
{
    ValueTask<Dictionary<string, object>?> GetUserProfileAsync(string userId,
        CancellationToken cancellationToken = default);

    ValueTask<Dictionary<string, object>?> FindUserByEmailAsync(string email,
        CancellationToken cancellationToken = default);

    ValueTask StorePendingChangesAsync(string userId, string requestId, Dictionary<string, object> changes,
        CancellationToken cancellationToken = default);

    ValueTask<Dictionary<string, object>?> GetPendingChangesAsync(string userId, string requestId,
        CancellationToken cancellationToken = default);

    ValueTask UpdateUserProfileAsync(string userId, Dictionary<string, object> changes,
        CancellationToken cancellationToken = default);

    ValueTask ClearPendingChangesAsync(string userId, string requestId, CancellationToken cancellationToken = default);
}
