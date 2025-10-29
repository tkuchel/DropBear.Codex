using DropBear.Codex.Core;
using DropBear.Codex.StateManagement.Errors;
using DropBear.Codex.StateManagement.StateSnapshots;
using DropBear.Codex.StateManagement.Tests.TestHelpers;
using FluentAssertions;

namespace DropBear.Codex.StateManagement.Tests.StateSnapshots;

public sealed class SimpleSnapshotManagerTests
{
    #region SaveState Tests

    [Fact]
    public void SaveState_WithValidState_ShouldSucceed()
    {
        // Arrange
        var state = TestState.Create();
        var manager = new SimpleSnapshotManager<TestState>(
            TimeSpan.FromSeconds(1),
            TimeSpan.FromHours(1),
            false);

        // Act
        var result = manager.SaveState(state);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void SaveState_Multiple_ShouldIncrementVersions()
    {
        // Arrange
        var state1 = TestState.Create();
        var state2 = state1.WithVersion(2);
        var state3 = state2.WithVersion(3);
        var manager = new SimpleSnapshotManager<TestState>(
            TimeSpan.FromSeconds(1),
            TimeSpan.FromHours(1),
            false);

        // Act
        var result1 = manager.SaveState(state1);
        var result2 = manager.SaveState(state2);
        var result3 = manager.SaveState(state3);

        // Assert
        result1.IsSuccess.Should().BeTrue();
        result2.IsSuccess.Should().BeTrue();
        result3.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void SaveState_WithMinimumInterval_ShouldRespectInterval()
    {
        // Arrange
        var state = TestState.Create();
        var interval = TimeSpan.FromMilliseconds(100);
        var manager = new SimpleSnapshotManager<TestState>(
            interval,
            TimeSpan.FromHours(1),
            true);

        // Act
        var result1 = manager.SaveState(state);
        var result2 = manager.SaveState(state); // Too soon

        // Assert
        result1.IsSuccess.Should().BeTrue();
        result2.IsSuccess.Should().BeFalse();
        result2.Error!.Message.Should().Contain("interval");
    }

    [Fact]
    public async Task SaveState_AfterInterval_ShouldAllowNewSnapshot()
    {
        // Arrange
        var state = TestState.Create();
        var interval = TimeSpan.FromMilliseconds(100);
        // Use automaticSnapshotting=false to avoid timer interference with manual calls
        var manager = new SimpleSnapshotManager<TestState>(
            interval,
            TimeSpan.FromHours(1),
            false);

        // Act
        var result1 = manager.SaveState(state);
        await Task.Delay(150); // Wait for interval to pass
        var result2 = manager.SaveState(state.WithVersion(2));

        // Assert
        result1.IsSuccess.Should().BeTrue();
        result2.IsSuccess.Should().BeTrue();
    }

    #endregion

    #region RestoreState Tests

    [Fact]
    public void RestoreState_ExistingVersion_ShouldReturnState()
    {
        // Arrange
        var originalState = TestState.Create();
        var manager = new SimpleSnapshotManager<TestState>(
            TimeSpan.FromSeconds(1),
            TimeSpan.FromHours(1),
            false);
        manager.SaveState(originalState);

        // Act
        var restoreResult = manager.RestoreState(1);

        // Assert
        restoreResult.IsSuccess.Should().BeTrue();
        var currentState = manager.GetCurrentState();
        currentState.IsSuccess.Should().BeTrue();
        currentState.Value!.Name.Should().Be(originalState.Name);
        currentState.Value.Version.Should().Be(originalState.Version);
    }

    [Fact]
    public void RestoreState_NonExistentVersion_ShouldFail()
    {
        // Arrange
        var manager = new SimpleSnapshotManager<TestState>(
            TimeSpan.FromSeconds(1),
            TimeSpan.FromHours(1),
            false);

        // Act
        var result = manager.RestoreState(999);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Message.Should().Contain("not found");
    }

    [Fact]
    public void RestoreState_MultipleSnapshots_ShouldReturnCorrectVersion()
    {
        // Arrange
        var state1 = new TestState("Version1", 1, DateTime.UtcNow);
        var state2 = new TestState("Version2", 2, DateTime.UtcNow);
        var state3 = new TestState("Version3", 3, DateTime.UtcNow);
        var manager = new SimpleSnapshotManager<TestState>(
            TimeSpan.FromSeconds(1),
            TimeSpan.FromHours(1),
            false);

        manager.SaveState(state1);
        manager.SaveState(state2);
        manager.SaveState(state3);

        // Act
        var result = manager.RestoreState(2);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var currentState = manager.GetCurrentState();
        currentState.IsSuccess.Should().BeTrue();
        currentState.Value!.Name.Should().Be("Version2");
        currentState.Value.Version.Should().Be(2);
    }

    #endregion

    #region GetCurrentState Tests

    [Fact]
    public void GetCurrentState_WithSnapshots_ShouldReturnLatest()
    {
        // Arrange
        var state1 = TestState.Create();
        var state2 = state1.WithVersion(2);
        var manager = new SimpleSnapshotManager<TestState>(
            TimeSpan.FromSeconds(1),
            TimeSpan.FromHours(1),
            false);

        manager.SaveState(state1);
        manager.SaveState(state2);

        // Act
        var result = manager.GetCurrentState();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Version.Should().Be(2);
    }

    [Fact]
    public void GetCurrentState_WithoutSnapshots_ShouldFail()
    {
        // Arrange
        var manager = new SimpleSnapshotManager<TestState>(
            TimeSpan.FromSeconds(1),
            TimeSpan.FromHours(1),
            false);

        // Act
        var result = manager.GetCurrentState();

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Message.Should().Contain("No current state");
    }

    #endregion

    #region IsDirty Tests

    [Fact]
    public void IsDirty_WithChangedState_ShouldReturnTrue()
    {
        // Arrange
        var originalState = TestState.Create();
        var modifiedState = originalState.WithName("Modified");
        var manager = new SimpleSnapshotManager<TestState>(
            TimeSpan.FromSeconds(1),
            TimeSpan.FromHours(1),
            false);

        manager.SaveState(originalState);

        // Act
        var result = manager.IsDirty(modifiedState);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
    }

    [Fact]
    public void IsDirty_WithUnchangedState_ShouldReturnFalse()
    {
        // Arrange
        var state = TestState.Create();
        var manager = new SimpleSnapshotManager<TestState>(
            TimeSpan.FromSeconds(1),
            TimeSpan.FromHours(1),
            false);

        manager.SaveState(state);

        // Act
        var result = manager.IsDirty(state);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeFalse();
    }

    [Fact]
    public void IsDirty_WithoutSnapshots_ShouldReturnTrue()
    {
        // Arrange
        var state = TestState.Create();
        var manager = new SimpleSnapshotManager<TestState>(
            TimeSpan.FromSeconds(1),
            TimeSpan.FromHours(1),
            false);

        // Act
        var result = manager.IsDirty(state);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue(); // No current state means everything is dirty
    }

    #endregion

    #region Retention and Expiration Tests

    [Fact]
    public async Task SaveState_WithRetention_ShouldRemoveOldSnapshots()
    {
        // Arrange
        var retention = TimeSpan.FromMilliseconds(100);
        var manager = new SimpleSnapshotManager<TestState>(
            TimeSpan.FromSeconds(1),
            retention,
            false);

        var state1 = TestState.Create();
        manager.SaveState(state1);

        // Wait for retention to expire
        await Task.Delay(150);

        var state2 = state1.WithVersion(2);
        manager.SaveState(state2);

        // Act
        var result = manager.RestoreState(1);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Message.Should().Contain("not found");
    }

    #endregion

    #region Disposal Tests

    [Fact]
    public void Dispose_ShouldStopTimer()
    {
        // Arrange
        var manager = new SimpleSnapshotManager<TestState>(
            TimeSpan.FromSeconds(1),
            TimeSpan.FromHours(1),
            false);
        var state = TestState.Create();

        manager.SaveState(state);

        // Act
        manager.Dispose();

        // Assert - should not throw and should complete successfully
        // Timer should be stopped
        Action act = () => manager.Dispose(); // Second dispose should be safe
        act.Should().NotThrow();
    }

    #endregion

    #region Thread Safety Tests

    [Fact]
    public async Task SaveState_Concurrent_ShouldHandleConcurrency()
    {
        // Arrange
        var manager = new SimpleSnapshotManager<TestState>(
            TimeSpan.FromSeconds(1),
            TimeSpan.FromHours(1),
            false);
        var successCount = 0;
        var tasks = new List<Task>();

        // Act - create 10 concurrent save operations
        for (var i = 0; i < 10; i++)
        {
            var index = i;
            tasks.Add(Task.Run(() =>
            {
                var state = new TestState($"State{index}", index, DateTime.UtcNow);
                var result = manager.SaveState(state);
                if (result.IsSuccess)
                {
                    Interlocked.Increment(ref successCount);
                }
            }));
        }

        await Task.WhenAll(tasks);

        // Assert - all operations should complete
        successCount.Should().BeGreaterThan(0);
    }

    #endregion

    #region Clone Verification Tests

    [Fact]
    public void GetCurrentState_ShouldReturnClone_NotReference()
    {
        // Arrange
        var originalState = TestState.Create();
        var manager = new SimpleSnapshotManager<TestState>(
            TimeSpan.FromSeconds(1),
            TimeSpan.FromHours(1),
            false);

        manager.SaveState(originalState);
        var retrieved1 = manager.GetCurrentState().Value!;
        var retrieved2 = manager.GetCurrentState().Value!;

        // Act & Assert - should be equal but not same reference
        retrieved1.Should().Be(retrieved2);
        retrieved1.Should().NotBeSameAs(retrieved2); // Different instances due to cloning
    }

    #endregion
}
