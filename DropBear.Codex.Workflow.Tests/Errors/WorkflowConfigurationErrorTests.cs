using DropBear.Codex.Core.Enums;
using DropBear.Codex.Core.Results.Errors;
using DropBear.Codex.Workflow.Errors;
using FluentAssertions;

namespace DropBear.Codex.Workflow.Tests.Errors;

public sealed class WorkflowConfigurationErrorTests
{
    #region InvalidConfiguration Tests

    [Fact]
    public void InvalidConfiguration_ShouldCreateError()
    {
        // Arrange
        var workflowId = "payment-workflow";
        var reason = "Invalid timeout value";

        // Act
        var error = WorkflowConfigurationError.InvalidConfiguration(workflowId, reason);

        // Assert
        error.Should().NotBeNull();
        error.Message.Should().Contain(workflowId);
        error.Message.Should().Contain(reason);
        error.WorkflowId.Should().Be(workflowId);
        error.Code.Should().Be("WF_INVALID_CONFIG");
        error.Category.Should().Be(ErrorCategory.Validation);
        error.Severity.Should().Be(ErrorSeverity.High);
    }

    #endregion

    #region MissingRequired Tests

    [Fact]
    public void MissingRequired_ShouldCreateError()
    {
        // Arrange
        var propertyName = "RootNode";

        // Act
        var error = WorkflowConfigurationError.MissingRequired(propertyName);

        // Assert
        error.Should().NotBeNull();
        error.Message.Should().Contain(propertyName);
        error.PropertyName.Should().Be(propertyName);
        error.Code.Should().Be("WF_MISSING_REQUIRED");
        error.Category.Should().Be(ErrorCategory.Validation);
        error.Severity.Should().Be(ErrorSeverity.High);
    }

    [Fact]
    public void MissingRequired_WithWorkflowId_ShouldIncludeWorkflowId()
    {
        // Arrange
        var workflowId = "user-onboarding";

        // Act
        var error = WorkflowConfigurationError.MissingRequired("StepDefinition", workflowId);

        // Assert
        error.WorkflowId.Should().Be(workflowId);
    }

    #endregion

    #region InvalidStep Tests

    [Fact]
    public void InvalidStep_ShouldCreateError()
    {
        // Arrange
        var stepName = "ValidationStep";
        var reason = "Missing required parameter";

        // Act
        var error = WorkflowConfigurationError.InvalidStep(stepName, reason);

        // Assert
        error.Should().NotBeNull();
        error.Message.Should().Contain(stepName);
        error.Message.Should().Contain(reason);
        error.PropertyName.Should().Be(stepName);
        error.Code.Should().Be("WF_INVALID_STEP");
        error.Category.Should().Be(ErrorCategory.Validation);
        error.Severity.Should().Be(ErrorSeverity.High);
    }

    [Fact]
    public void InvalidStep_WithWorkflowId_ShouldIncludeWorkflowId()
    {
        // Arrange
        var workflowId = "order-processing";

        // Act
        var error = WorkflowConfigurationError.InvalidStep("Step1", "Error", workflowId);

        // Assert
        error.WorkflowId.Should().Be(workflowId);
    }

    #endregion

    #region CircularDependency Tests

    [Fact]
    public void CircularDependency_ShouldCreateError()
    {
        // Arrange
        var workflowId = "complex-workflow";
        var details = "Step A -> Step B -> Step A";

        // Act
        var error = WorkflowConfigurationError.CircularDependency(workflowId, details);

        // Assert
        error.Should().NotBeNull();
        error.Message.Should().Contain(workflowId);
        error.Message.Should().Contain(details);
        error.WorkflowId.Should().Be(workflowId);
        error.Code.Should().Be("WF_CIRCULAR_DEPENDENCY");
        error.Category.Should().Be(ErrorCategory.Validation);
        error.Severity.Should().Be(ErrorSeverity.Critical);
    }

    #endregion

    #region InvalidBuilderState Tests

    [Fact]
    public void InvalidBuilderState_ShouldCreateError()
    {
        // Arrange
        var reason = "Cannot add step after workflow is built";

        // Act
        var error = WorkflowConfigurationError.InvalidBuilderState(reason);

        // Assert
        error.Should().NotBeNull();
        error.Message.Should().Contain(reason);
        error.Code.Should().Be("WF_INVALID_BUILDER");
        error.Category.Should().Be(ErrorCategory.Technical);
        error.Severity.Should().Be(ErrorSeverity.High);
    }

    #endregion
}
