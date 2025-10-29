using DropBear.Codex.Core.Enums;
using DropBear.Codex.Core.Results.Errors;
using DropBear.Codex.Workflow.Errors;
using FluentAssertions;

namespace DropBear.Codex.Workflow.Tests.Errors;

public sealed class WorkflowStepTimeoutErrorTests
{
    #region StepTimedOut Tests

    [Fact]
    public void StepTimedOut_ShouldCreateError()
    {
        // Arrange
        var stepName = "ProcessingStep";
        var timeout = TimeSpan.FromSeconds(30);

        // Act
        var error = WorkflowStepTimeoutError.StepTimedOut(stepName, timeout);

        // Assert
        error.Should().NotBeNull();
        error.Message.Should().Contain(stepName);
        error.Message.Should().Contain("30");
        error.StepName.Should().Be(stepName);
        error.Timeout.Should().Be(timeout);
        error.Code.Should().Be("WF_STEP_TIMEOUT");
        error.Category.Should().Be(ErrorCategory.Technical);
        error.Severity.Should().Be(ErrorSeverity.High);
    }

    [Fact]
    public void StepTimedOut_WithActualDuration_ShouldIncludeActualDuration()
    {
        // Arrange
        var timeout = TimeSpan.FromSeconds(30);
        var actualDuration = TimeSpan.FromSeconds(35);

        // Act
        var error = WorkflowStepTimeoutError.StepTimedOut("Step1", timeout, actualDuration);

        // Assert
        error.ActualDuration.Should().Be(actualDuration);
        error.Message.Should().Contain("35");
    }

    [Fact]
    public void StepTimedOut_WithWorkflowId_ShouldIncludeWorkflowId()
    {
        // Arrange
        var workflowId = "data-import";

        // Act
        var error = WorkflowStepTimeoutError.StepTimedOut("Step1", TimeSpan.FromSeconds(10), workflowId: workflowId);

        // Assert
        error.WorkflowId.Should().Be(workflowId);
    }

    #endregion

    #region WorkflowTimedOut Tests

    [Fact]
    public void WorkflowTimedOut_ShouldCreateError()
    {
        // Arrange
        var workflowId = "long-running-task";
        var timeout = TimeSpan.FromMinutes(5);

        // Act
        var error = WorkflowStepTimeoutError.WorkflowTimedOut(workflowId, timeout);

        // Assert
        error.Should().NotBeNull();
        error.Message.Should().Contain(workflowId);
        error.Message.Should().Contain("300");
        error.WorkflowId.Should().Be(workflowId);
        error.Timeout.Should().Be(timeout);
        error.Code.Should().Be("WF_TIMEOUT");
        error.Category.Should().Be(ErrorCategory.Technical);
        error.Severity.Should().Be(ErrorSeverity.Critical);
    }

    [Fact]
    public void WorkflowTimedOut_WithActualDuration_ShouldIncludeActualDuration()
    {
        // Arrange
        var timeout = TimeSpan.FromMinutes(5);
        var actualDuration = TimeSpan.FromMinutes(6);

        // Act
        var error = WorkflowStepTimeoutError.WorkflowTimedOut("workflow-1", timeout, actualDuration);

        // Assert
        error.ActualDuration.Should().Be(actualDuration);
        error.Message.Should().Contain("360");
    }

    #endregion
}
