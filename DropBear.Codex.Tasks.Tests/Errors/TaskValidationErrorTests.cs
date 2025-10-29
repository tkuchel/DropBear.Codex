using DropBear.Codex.Tasks.Errors;
using FluentAssertions;

namespace DropBear.Codex.Tasks.Tests.Errors;

public sealed class TaskValidationErrorTests
{
    #region Factory Method Tests

    [Fact]
    public void InvalidName_ShouldCreateError()
    {
        // Arrange
        var taskName = "TestTask";

        // Act
        var error = TaskValidationError.InvalidName(taskName);

        // Assert
        error.Should().NotBeNull();
        error.TaskName.Should().Be(taskName);
        error.Message.Should().Contain("Task name cannot be null or empty");
    }

    [Fact]
    public void InvalidProperty_ShouldCreateError()
    {
        // Arrange
        var taskName = "TestTask";
        var propertyName = "MaxRetryCount";
        var reason = "Cannot be negative";

        // Act
        var error = TaskValidationError.InvalidProperty(taskName, propertyName, reason);

        // Assert
        error.Should().NotBeNull();
        error.TaskName.Should().Be(taskName);
        error.Message.Should().Contain(propertyName);
        error.Message.Should().Contain(reason);
    }

    #endregion

    #region Inheritance Tests

    [Fact]
    public void TaskValidationError_ShouldInheritFromTaskExecutionError()
    {
        // Arrange & Act
        var error = TaskValidationError.InvalidName("Task");

        // Assert
        error.Should().BeAssignableTo<TaskExecutionError>();
    }

    #endregion
}
