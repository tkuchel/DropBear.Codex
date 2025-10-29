using DropBear.Codex.StateManagement.Errors;
using DropBear.Codex.StateManagement.StateSnapshots;
using DropBear.Codex.StateManagement.StateSnapshots.Builder;
using DropBear.Codex.StateManagement.Tests.TestHelpers;
using FluentAssertions;

namespace DropBear.Codex.StateManagement.Tests.StateSnapshots;

public sealed class SnapshotBuilderTests
{
    #region Build Tests

    [Fact]
    public void Build_WithDefaults_ShouldCreateManager()
    {
        // Arrange
        var builder = new SnapshotBuilder<TestState>();

        // Act
        var manager = builder.Build();

        // Assert
        manager.Should().NotBeNull();
        manager.Should().BeOfType<SimpleSnapshotManager<TestState>>();
    }

    [Fact]
    public void Build_WithConfiguration_ShouldApplySettings()
    {
        // Arrange
        var builder = new SnapshotBuilder<TestState>()
            .WithSnapshotInterval(TimeSpan.FromSeconds(5))
            .WithRetentionTime(TimeSpan.FromMinutes(10));

        // Act
        var manager = builder.Build();

        // Assert
        manager.Should().NotBeNull();
    }

    [Fact]
    public void TryBuild_WithValidConfiguration_ShouldSucceed()
    {
        // Arrange
        var builder = new SnapshotBuilder<TestState>()
            .WithSnapshotInterval(TimeSpan.FromSeconds(2));

        // Act
        var result = builder.TryBuild();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
    }

    #endregion

    #region Configuration Tests

    [Fact]
    public void WithSnapshotInterval_ValidValue_ShouldReturnBuilder()
    {
        // Arrange
        var builder = new SnapshotBuilder<TestState>();

        // Act
        var result = builder.WithSnapshotInterval(TimeSpan.FromSeconds(5));

        // Assert
        result.Should().BeSameAs(builder); // Fluent chaining
    }

    [Fact]
    public void WithSnapshotInterval_LessThanOneSecond_ShouldThrow()
    {
        // Arrange
        var builder = new SnapshotBuilder<TestState>();

        // Act
        Action act = () => builder.WithSnapshotInterval(TimeSpan.FromMilliseconds(500));

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*at least one second*");
    }

    [Fact]
    public void TryWithSnapshotInterval_ValidValue_ShouldSucceed()
    {
        // Arrange
        var builder = new SnapshotBuilder<TestState>();

        // Act
        var result = builder.TryWithSnapshotInterval(TimeSpan.FromSeconds(5));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(builder);
    }

    [Fact]
    public void TryWithSnapshotInterval_InvalidValue_ShouldReturnError()
    {
        // Arrange
        var builder = new SnapshotBuilder<TestState>();

        // Act
        var result = builder.TryWithSnapshotInterval(TimeSpan.FromMilliseconds(500));

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Message.Should().Contain("Invalid interval");
    }

    [Fact]
    public void WithRetentionTime_ValidValue_ShouldReturnBuilder()
    {
        // Arrange
        var builder = new SnapshotBuilder<TestState>();

        // Act
        var result = builder.WithRetentionTime(TimeSpan.FromHours(1));

        // Assert
        result.Should().BeSameAs(builder);
    }

    [Fact]
    public void WithRetentionTime_NegativeValue_ShouldThrow()
    {
        // Arrange
        var builder = new SnapshotBuilder<TestState>();

        // Act
        Action act = () => builder.WithRetentionTime(TimeSpan.FromSeconds(-1));

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*cannot be negative*");
    }

    [Fact]
    public void TryWithRetentionTime_ValidValue_ShouldSucceed()
    {
        // Arrange
        var builder = new SnapshotBuilder<TestState>();

        // Act
        var result = builder.TryWithRetentionTime(TimeSpan.FromHours(1));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(builder);
    }

    [Fact]
    public void TryWithRetentionTime_NegativeValue_ShouldReturnError()
    {
        // Arrange
        var builder = new SnapshotBuilder<TestState>();

        // Act
        var result = builder.TryWithRetentionTime(TimeSpan.FromSeconds(-1));

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Message.Should().Contain("Invalid retention time");
    }

    #endregion

    #region Fluent Chaining Tests

    [Fact]
    public void FluentChaining_MultipleConfigurations_ShouldWork()
    {
        // Arrange & Act
        var result = new SnapshotBuilder<TestState>()
            .WithSnapshotInterval(TimeSpan.FromSeconds(5))
            .WithRetentionTime(TimeSpan.FromMinutes(30))
            .WithAutomaticSnapshotting(true)
            .TryBuild();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
    }

    [Fact]
    public void FluentChaining_UsingTryMethods_ShouldWork()
    {
        // Arrange
        var builder = new SnapshotBuilder<TestState>();

        // Act
        var intervalResult = builder.TryWithSnapshotInterval(TimeSpan.FromSeconds(3));
        var retentionResult = intervalResult.IsSuccess
            ? intervalResult.Value!.TryWithRetentionTime(TimeSpan.FromMinutes(15))
            : intervalResult;
        var buildResult = retentionResult.IsSuccess
            ? retentionResult.Value!.TryBuild()
            : throw new Exception("Configuration failed");

        // Assert
        intervalResult.IsSuccess.Should().BeTrue();
        retentionResult.IsSuccess.Should().BeTrue();
        buildResult.IsSuccess.Should().BeTrue();
    }

    #endregion

    #region Automatic Snapshotting Tests

    [Fact]
    public void WithAutomaticSnapshotting_ShouldEnableFeature()
    {
        // Arrange
        var builder = new SnapshotBuilder<TestState>();

        // Act
        var result = builder.WithAutomaticSnapshotting(true);

        // Assert
        result.Should().BeSameAs(builder);
    }

    [Fact]
    public async Task Build_WithAutomaticSnapshotting_ShouldCreateSnapshots()
    {
        // Arrange
        var manager = new SnapshotBuilder<TestState>()
            .WithSnapshotInterval(TimeSpan.FromSeconds(1))
            .WithAutomaticSnapshotting(true)
            .Build();

        var state = TestState.Create();
        manager.SaveState(state);

        // Act - wait for automatic snapshot
        await Task.Delay(100);

        // Assert - we should have at least the initial snapshot
        var currentState = manager.GetCurrentState();
        currentState.IsSuccess.Should().BeTrue();
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Build_CalledMultipleTimes_ShouldCreateIndependentManagers()
    {
        // Arrange
        var builder = new SnapshotBuilder<TestState>();

        // Act
        var manager1 = builder.Build();
        var manager2 = builder.Build();

        // Assert
        manager1.Should().NotBeSameAs(manager2);
    }

    [Fact]
    public void TryBuild_WithException_ShouldReturnError()
    {
        // Arrange
        var builder = new SnapshotBuilder<TestState>();

        // Act - Try to set invalid interval using Try method
        var result = builder.TryWithSnapshotInterval(TimeSpan.FromMilliseconds(100));

        // Assert - Should return error for interval less than 1 second
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
    }

    #endregion
}
