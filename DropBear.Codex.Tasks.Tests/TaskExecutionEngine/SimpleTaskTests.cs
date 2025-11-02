using DropBear.Codex.Tasks.Errors;
using DropBear.Codex.Tasks.TaskExecutionEngine;
using DropBear.Codex.Tasks.TaskExecutionEngine.Interfaces;
using DropBear.Codex.Tasks.TaskExecutionEngine.Models;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ExecutionContext = DropBear.Codex.Tasks.TaskExecutionEngine.Models.ExecutionContext;

namespace DropBear.Codex.Tasks.Tests.TaskExecutionEngine;

public sealed class SimpleTaskTests
{
    #region Construction Tests

    [Fact]
    public void Constructor_WithAsyncDelegate_ShouldCreateTask()
    {
        // Arrange & Act
        var task = new SimpleTask("TestTask", async (ctx, ct) => await Task.CompletedTask);

        // Assert
        task.Should().NotBeNull();
        task.Name.Should().Be("TestTask");
    }

    [Fact]
    public void Constructor_WithSyncDelegate_ShouldCreateTask()
    {
        // Arrange & Act
        var task = new SimpleTask("TestTask", ctx => { });

        // Assert
        task.Should().NotBeNull();
        task.Name.Should().Be("TestTask");
    }

    [Fact]
    public void Constructor_WithNullName_ShouldThrow()
    {
        // Arrange & Act
        Action act = () => new SimpleTask(null!, async (ctx, ct) => await Task.CompletedTask);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullAsyncDelegate_ShouldThrow()
    {
        // Arrange & Act
        Action act = () => new SimpleTask("TestTask", (Func<ExecutionContext, CancellationToken, Task>)null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullSyncDelegate_ShouldThrow()
    {
        // Arrange & Act
        Action act = () => new SimpleTask("TestTask", (Action<ExecutionContext>)null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region Validation Tests

    [Fact]
    public void Validate_WithValidConfiguration_ShouldSucceed()
    {
        // Arrange
        var task = new SimpleTask("TestTask", ctx => { });

        // Act
        var result = task.Validate();

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithEmptyName_ShouldFail()
    {
        // Arrange
        var task = new SimpleTask("", ctx => { });

        // Act
        var result = task.Validate();

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
    }

    [Fact]
    public void Validate_WithNegativeMaxRetryCount_ShouldFail()
    {
        // Arrange
        var task = new SimpleTask("TestTask", ctx => { })
        {
            MaxRetryCount = -1
        };

        // Act
        var result = task.Validate();

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Message.Should().Contain("MaxRetryCount");
    }

    [Fact]
    public void Validate_WithNegativeTimeout_ShouldFail()
    {
        // Arrange
        var task = new SimpleTask("TestTask", ctx => { })
        {
            Timeout = TimeSpan.FromSeconds(-1)
        };

        // Act
        var result = task.Validate();

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Message.Should().Contain("Timeout");
    }

    #endregion

    #region Execution Tests

    [Fact]
    public async Task ExecuteAsync_WithSuccessfulExecution_ShouldReturnSuccess()
    {
        // Arrange
        var executed = false;
        var task = new SimpleTask("TestTask", ctx => { executed = true; });
        var context = CreateTestContext();

        // Act
        var result = await task.ExecuteAsync(context, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        executed.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WithAsyncExecution_ShouldReturnSuccess()
    {
        // Arrange
        var executed = false;
        var task = new SimpleTask("TestTask", async (ctx, ct) =>
        {
            await Task.Delay(10, ct);
            executed = true;
        });
        var context = CreateTestContext();

        // Act
        var result = await task.ExecuteAsync(context, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        executed.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WithException_ShouldReturnFailure()
    {
        // Arrange
        var task = new SimpleTask("TestTask", ctx => throw new InvalidOperationException("Test error"));
        var context = CreateTestContext();

        // Act
        var result = await task.ExecuteAsync(context, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Message.Should().Contain("Test error");
    }

    [Fact]
    public async Task ExecuteAsync_WithCancellation_ShouldReturnCancelled()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();
        var task = new SimpleTask("TestTask", async (ctx, ct) =>
        {
            await Task.Delay(1000, ct);
        });
        var context = CreateTestContext();

        // Act
        var result = await task.ExecuteAsync(context, cts.Token);

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    #endregion

    #region Dependency Tests

    [Fact]
    public void AddDependency_ShouldAddDependency()
    {
        // Arrange
        var task = new SimpleTask("TestTask", ctx => { });

        // Act
        task.AddDependency("Dependency1");

        // Assert
        task.HasDependency("Dependency1").Should().BeTrue();
        task.Dependencies.Should().Contain("Dependency1");
    }

    [Fact]
    public void AddDependency_WithNull_ShouldThrow()
    {
        // Arrange
        var task = new SimpleTask("TestTask", ctx => { });

        // Act
        Action act = () => task.AddDependency(null!);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AddDependency_WithWhitespace_ShouldThrow()
    {
        // Arrange
        var task = new SimpleTask("TestTask", ctx => { });

        // Act
        Action act = () => task.AddDependency("   ");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SetDependencies_ShouldReplaceDependencies()
    {
        // Arrange
        var task = new SimpleTask("TestTask", ctx => { });
        task.AddDependency("Old");

        // Act
        task.SetDependencies(new[] { "New1", "New2" });

        // Assert
        task.HasDependency("Old").Should().BeFalse();
        task.HasDependency("New1").Should().BeTrue();
        task.HasDependency("New2").Should().BeTrue();
    }

    [Fact]
    public void RemoveDependency_ShouldRemoveDependency()
    {
        // Arrange
        var task = new SimpleTask("TestTask", ctx => { });
        task.AddDependency("Dep1");

        // Act
        task.RemoveDependency("Dep1");

        // Assert
        task.HasDependency("Dep1").Should().BeFalse();
    }

    [Fact]
    public void HasDependency_WithNonExistentDependency_ShouldReturnFalse()
    {
        // Arrange
        var task = new SimpleTask("TestTask", ctx => { });

        // Act & Assert
        task.HasDependency("NonExistent").Should().BeFalse();
    }

    #endregion

    #region Metadata Tests

    [Fact]
    public void Metadata_ShouldBeInitiallyEmpty()
    {
        // Arrange & Act
        var task = new SimpleTask("TestTask", ctx => { });

        // Assert
        task.Metadata.Should().NotBeNull();
        task.Metadata.Should().BeEmpty();
    }

    [Fact]
    public void Metadata_CanAddAndRetrieve()
    {
        // Arrange
        var task = new SimpleTask("TestTask", ctx => { });

        // Act
        task.Metadata["Key1"] = "Value1";
        task.Metadata["Key2"] = 42;

        // Assert
        task.Metadata["Key1"].Should().Be("Value1");
        task.Metadata["Key2"].Should().Be(42);
    }

    #endregion

    #region Helper Methods

    private static ExecutionContext CreateTestContext()
    {
        var mockScopeFactory = new Mock<IServiceScopeFactory>();
        var mockLogger = new Mock<Microsoft.Extensions.Logging.ILogger<ExecutionContext>>();
        return new ExecutionContext(
            new ExecutionOptions(),
            mockScopeFactory.Object,
            mockLogger.Object);
    }

    #endregion
}
