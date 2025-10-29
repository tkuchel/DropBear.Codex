using DropBear.Codex.Tasks.Errors;
using FluentAssertions;

namespace DropBear.Codex.Tasks.Tests.Errors;

public sealed class TaskExecutionErrorTests
{
    #region Factory Method Tests

    [Fact]
    public void Timeout_ShouldCreateError()
    {
        // Arrange
        var taskName = "TestTask";
        var timeout = TimeSpan.FromSeconds(30);

        // Act
        var error = TaskExecutionError.Timeout(taskName, timeout);

        // Assert
        error.Should().NotBeNull();
        error.TaskName.Should().Be(taskName);
        error.Message.Should().Contain("TestTask");
        error.Message.Should().Contain("30");
    }

    [Fact]
    public void Failed_WithException_ShouldCreateError()
    {
        // Arrange
        var taskName = "TestTask";
        var exception = new InvalidOperationException("Test error");

        // Act
        var error = TaskExecutionError.Failed(taskName, exception);

        // Assert
        error.Should().NotBeNull();
        error.TaskName.Should().Be(taskName);
        error.Exception.Should().Be(exception);
        error.Message.Should().Contain("TestTask");
        error.Message.Should().Contain("Test error");
    }

    [Fact]
    public void Cancelled_ShouldCreateError()
    {
        // Arrange
        var taskName = "TestTask";

        // Act
        var error = TaskExecutionError.Cancelled(taskName);

        // Assert
        error.Should().NotBeNull();
        error.TaskName.Should().Be(taskName);
        error.Message.Should().Contain("TestTask");
        error.Message.Should().Contain("cancelled");
    }

    #endregion

    #region Properties Tests

    [Fact]
    public void TaskName_ShouldBeSet()
    {
        // Arrange & Act
        var error = TaskExecutionError.Failed("MyTask", new Exception("Error"));

        // Assert
        error.TaskName.Should().Be("MyTask");
    }

    [Fact]
    public void Exception_ShouldBeSet()
    {
        // Arrange
        var exception = new InvalidOperationException("Test");

        // Act
        var error = TaskExecutionError.Failed("Task", exception);

        // Assert
        error.Exception.Should().Be(exception);
    }

    #endregion
}
