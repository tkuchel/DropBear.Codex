using DropBear.Codex.Tasks.TaskExecutionEngine;
using DropBear.Codex.Tasks.TaskExecutionEngine.Enums;
using FluentAssertions;
using ExecutionContext = DropBear.Codex.Tasks.TaskExecutionEngine.Models.ExecutionContext;

namespace DropBear.Codex.Tasks.Tests.TaskExecutionEngine;

public sealed class TaskBuilderTests
{
    #region Construction Tests

    [Fact]
    public void Create_WithName_ShouldReturnBuilder()
    {
        // Arrange & Act
        var builder = TaskBuilder.Create("TestTask");

        // Assert
        builder.Should().NotBeNull();
    }

    [Fact]
    public void Create_WithNullName_ShouldThrow()
    {
        // Arrange & Act
        Action act = () => TaskBuilder.Create(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region Fluent Configuration Tests

    [Fact]
    public void WithExecution_WithAsyncDelegate_ShouldReturnBuilder()
    {
        // Arrange
        var builder = TaskBuilder.Create("TestTask");

        // Act
        var result = builder.WithExecution(async (ctx, ct) => await Task.CompletedTask);

        // Assert
        result.Should().BeSameAs(builder);
    }

    [Fact]
    public void WithExecution_WithSyncDelegate_ShouldReturnBuilder()
    {
        // Arrange
        var builder = TaskBuilder.Create("TestTask");

        // Act
        var result = builder.WithExecution(ctx => { });

        // Assert
        result.Should().BeSameAs(builder);
    }

    [Fact]
    public void WithMaxRetryCount_ShouldSetValue()
    {
        // Arrange
        var builder = TaskBuilder.Create("TestTask");

        // Act
        var result = builder.WithMaxRetryCount(3);

        // Assert
        result.Should().BeSameAs(builder);
    }

    [Fact]
    public void WithMaxRetryCount_WithNegativeValue_ShouldThrow()
    {
        // Arrange
        var builder = TaskBuilder.Create("TestTask");

        // Act
        Action act = () => builder.WithMaxRetryCount(-1);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void WithRetryDelay_ShouldSetValue()
    {
        // Arrange
        var builder = TaskBuilder.Create("TestTask");

        // Act
        var result = builder.WithRetryDelay(TimeSpan.FromSeconds(2));

        // Assert
        result.Should().BeSameAs(builder);
    }

    [Fact]
    public void WithRetryDelay_WithNegativeValue_ShouldThrow()
    {
        // Arrange
        var builder = TaskBuilder.Create("TestTask");

        // Act
        Action act = () => builder.WithRetryDelay(TimeSpan.FromSeconds(-1));

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void WithTimeout_ShouldSetValue()
    {
        // Arrange
        var builder = TaskBuilder.Create("TestTask");

        // Act
        var result = builder.WithTimeout(TimeSpan.FromMinutes(5));

        // Assert
        result.Should().BeSameAs(builder);
    }

    [Fact]
    public void WithTimeout_WithZeroOrNegative_ShouldThrow()
    {
        // Arrange
        var builder = TaskBuilder.Create("TestTask");

        // Act
        Action act = () => builder.WithTimeout(TimeSpan.Zero);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void WithDependencies_ShouldSetDependencies()
    {
        // Arrange
        var builder = TaskBuilder.Create("TestTask");

        // Act
        var result = builder.WithDependencies(new[] { "Dep1", "Dep2" });

        // Assert
        result.Should().BeSameAs(builder);
    }

    [Fact]
    public void WithCondition_ShouldSetCondition()
    {
        // Arrange
        var builder = TaskBuilder.Create("TestTask");

        // Act
        var result = builder.WithCondition(ctx => true);

        // Assert
        result.Should().BeSameAs(builder);
    }

    [Fact]
    public void WithCompensationAction_ShouldSetAction()
    {
        // Arrange
        var builder = TaskBuilder.Create("TestTask");

        // Act
        var result = builder.WithCompensationAction(async ctx => await Task.CompletedTask);

        // Assert
        result.Should().BeSameAs(builder);
    }

    [Fact]
    public void WithMetadata_ShouldAddMetadata()
    {
        // Arrange
        var builder = TaskBuilder.Create("TestTask");

        // Act
        var result = builder.WithMetadata("Key1", "Value1");

        // Assert
        result.Should().BeSameAs(builder);
    }

    [Fact]
    public void WithPriority_ShouldSetPriority()
    {
        // Arrange
        var builder = TaskBuilder.Create("TestTask");

        // Act
        var result = builder.WithPriority(TaskPriority.High);

        // Assert
        result.Should().BeSameAs(builder);
    }

    [Fact]
    public void WithEstimatedDuration_ShouldSetDuration()
    {
        // Arrange
        var builder = TaskBuilder.Create("TestTask");

        // Act
        var result = builder.WithEstimatedDuration(TimeSpan.FromMinutes(10));

        // Assert
        result.Should().BeSameAs(builder);
    }

    [Fact]
    public void WithEstimatedDuration_WithNegativeValue_ShouldThrow()
    {
        // Arrange
        var builder = TaskBuilder.Create("TestTask");

        // Act
        Action act = () => builder.WithEstimatedDuration(TimeSpan.FromSeconds(-1));

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ContinueOnFailure_ShouldSetFlag()
    {
        // Arrange
        var builder = TaskBuilder.Create("TestTask");

        // Act
        var result = builder.ContinueOnFailure(true);

        // Assert
        result.Should().BeSameAs(builder);
    }

    #endregion

    #region Build Tests

    [Fact]
    public void Build_WithMinimalConfiguration_ShouldCreateTask()
    {
        // Arrange
        var builder = TaskBuilder.Create("TestTask")
            .WithExecution(ctx => { });

        // Act
        var task = builder.Build();

        // Assert
        task.Should().NotBeNull();
        task.Name.Should().Be("TestTask");
    }

    [Fact]
    public void Build_WithFullConfiguration_ShouldCreateTask()
    {
        // Arrange
        var builder = TaskBuilder.Create("TestTask")
            .WithExecution(async (ctx, ct) => await Task.CompletedTask)
            .WithMaxRetryCount(3)
            .WithRetryDelay(TimeSpan.FromSeconds(2))
            .WithTimeout(TimeSpan.FromMinutes(5))
            .WithDependencies(new[] { "Dep1" })
            .WithCondition(ctx => true)
            .WithCompensationAction(async ctx => await Task.CompletedTask)
            .WithMetadata("Key", "Value")
            .WithPriority(TaskPriority.High)
            .WithEstimatedDuration(TimeSpan.FromMinutes(10))
            .ContinueOnFailure(true);

        // Act
        var task = builder.Build();

        // Assert
        task.Should().NotBeNull();
        task.Name.Should().Be("TestTask");
        task.MaxRetryCount.Should().Be(3);
        task.RetryDelay.Should().Be(TimeSpan.FromSeconds(2));
        task.Timeout.Should().Be(TimeSpan.FromMinutes(5));
        task.Dependencies.Should().Contain("Dep1");
        task.Priority.Should().Be(TaskPriority.High);
        task.EstimatedDuration.Should().Be(TimeSpan.FromMinutes(10));
        task.ContinueOnFailure.Should().BeTrue();
        task.Metadata["Key"].Should().Be("Value");
    }

    [Fact]
    public void Build_WithoutExecution_ShouldThrow()
    {
        // Arrange
        var builder = TaskBuilder.Create("TestTask");

        // Act
        Action act = () => builder.Build();

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    #endregion

    #region Fluent Chaining Tests

    [Fact]
    public void FluentChaining_MultipleConfigurations_ShouldWork()
    {
        // Arrange & Act
        var task = TaskBuilder.Create("TestTask")
            .WithExecution(ctx => { })
            .WithMaxRetryCount(2)
            .WithTimeout(TimeSpan.FromSeconds(30))
            .WithPriority(TaskPriority.High)
            .ContinueOnFailure(false)
            .Build();

        // Assert
        task.Should().NotBeNull();
        task.Name.Should().Be("TestTask");
        task.MaxRetryCount.Should().Be(2);
        task.Timeout.Should().Be(TimeSpan.FromSeconds(30));
        task.Priority.Should().Be(TaskPriority.High);
        task.ContinueOnFailure.Should().BeFalse();
    }

    #endregion
}
