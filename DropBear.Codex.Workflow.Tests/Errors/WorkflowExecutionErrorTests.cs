using DropBear.Codex.Core.Enums;
using DropBear.Codex.Core.Results.Errors;
using DropBear.Codex.Workflow.Errors;
using FluentAssertions;

namespace DropBear.Codex.Workflow.Tests.Errors;

public sealed class WorkflowExecutionErrorTests
{
    #region StepFailed Tests

    [Fact]
    public void StepFailed_ShouldCreateError()
    {
        // Arrange
        var stepName = "ValidationStep";
        var reason = "Invalid input";

        // Act
        var error = WorkflowExecutionError.StepFailed(stepName, reason);

        // Assert
        error.Should().NotBeNull();
        error.Message.Should().Contain(stepName);
        error.Message.Should().Contain(reason);
        error.StepName.Should().Be(stepName);
        error.Code.Should().Be("WF_STEP_FAILED");
        error.Category.Should().Be(ErrorCategory.Technical);
        error.Severity.Should().Be(ErrorSeverity.High);
    }

    [Fact]
    public void StepFailed_WithWorkflowId_ShouldIncludeWorkflowId()
    {
        // Arrange
        var workflowId = "order-processing";

        // Act
        var error = WorkflowExecutionError.StepFailed("Step1", "Error", workflowId);

        // Assert
        error.WorkflowId.Should().Be(workflowId);
    }

    [Fact]
    public void StepFailed_WithCorrelationId_ShouldIncludeCorrelationId()
    {
        // Arrange
        var correlationId = Guid.NewGuid().ToString();

        // Act
        var error = WorkflowExecutionError.StepFailed("Step1", "Error", correlationId: correlationId);

        // Assert
        error.CorrelationId.Should().Be(correlationId);
    }

    #endregion

    #region ExecutionFailed Tests

    [Fact]
    public void ExecutionFailed_ShouldCreateError()
    {
        // Arrange
        var workflowId = "user-registration";
        var reason = "Database connection failed";

        // Act
        var error = WorkflowExecutionError.ExecutionFailed(workflowId, reason);

        // Assert
        error.Should().NotBeNull();
        error.Message.Should().Contain(workflowId);
        error.Message.Should().Contain(reason);
        error.WorkflowId.Should().Be(workflowId);
        error.Code.Should().Be("WF_EXEC_FAILED");
        error.Category.Should().Be(ErrorCategory.Technical);
        error.Severity.Should().Be(ErrorSeverity.Critical);
    }

    [Fact]
    public void ExecutionFailed_WithCorrelationId_ShouldIncludeCorrelationId()
    {
        // Arrange
        var correlationId = Guid.NewGuid().ToString();

        // Act
        var error = WorkflowExecutionError.ExecutionFailed("workflow-1", "Error", correlationId);

        // Assert
        error.CorrelationId.Should().Be(correlationId);
    }

    #endregion

    #region CompensationFailed Tests

    [Fact]
    public void CompensationFailed_ShouldCreateError()
    {
        // Arrange
        var stepName = "PaymentStep";
        var reason = "Refund API unavailable";

        // Act
        var error = WorkflowExecutionError.CompensationFailed(stepName, reason);

        // Assert
        error.Should().NotBeNull();
        error.Message.Should().Contain(stepName);
        error.Message.Should().Contain(reason);
        error.StepName.Should().Be(stepName);
        error.Code.Should().Be("WF_COMPENSATION_FAILED");
        error.Category.Should().Be(ErrorCategory.Technical);
        error.Severity.Should().Be(ErrorSeverity.Critical);
    }

    [Fact]
    public void CompensationFailed_WithWorkflowId_ShouldIncludeWorkflowId()
    {
        // Arrange
        var workflowId = "order-processing";

        // Act
        var error = WorkflowExecutionError.CompensationFailed("Step1", "Error", workflowId);

        // Assert
        error.WorkflowId.Should().Be(workflowId);
    }

    #endregion

    #region Cancelled Tests

    [Fact]
    public void Cancelled_ShouldCreateError()
    {
        // Arrange
        var workflowId = "long-running-task";

        // Act
        var error = WorkflowExecutionError.Cancelled(workflowId);

        // Assert
        error.Should().NotBeNull();
        error.Message.Should().Contain(workflowId);
        error.Message.Should().Contain("cancelled");
        error.WorkflowId.Should().Be(workflowId);
        error.Code.Should().Be("WF_CANCELLED");
        error.Category.Should().Be(ErrorCategory.Cancelled);
        error.Severity.Should().Be(ErrorSeverity.Low);
    }

    [Fact]
    public void Cancelled_WithCorrelationId_ShouldIncludeCorrelationId()
    {
        // Arrange
        var correlationId = Guid.NewGuid().ToString();

        // Act
        var error = WorkflowExecutionError.Cancelled("workflow-1", correlationId);

        // Assert
        error.CorrelationId.Should().Be(correlationId);
    }

    #endregion
}
