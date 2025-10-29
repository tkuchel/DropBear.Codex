using DropBear.Codex.StateManagement.StateMachine.Builder;
using FluentAssertions;

namespace DropBear.Codex.StateManagement.Tests.StateMachine.Builder;

public sealed class StateMachineBuilderTests
{
    #region Test Enums

    public enum TestState
    {
        Initial,
        Processing,
        Completed,
        Failed
    }

    public enum TestTrigger
    {
        Start,
        Complete,
        Fail,
        Reset
    }

    #endregion

    #region Build Tests

    [Fact]
    public void Build_WithBasicConfiguration_ShouldCreateStateMachine()
    {
        // Arrange
        var builder = new StateMachineBuilder<TestState, TestTrigger>(TestState.Initial);

        builder
            .ConfigureState(TestState.Initial)
            .Permit(TestTrigger.Start, TestState.Processing);

        // Act
        var stateMachine = builder.Build();

        // Assert
        stateMachine.Should().NotBeNull();
        stateMachine.State.Should().Be(TestState.Initial);
    }

    [Fact]
    public void BuildSafe_FirstCall_ShouldSucceed()
    {
        // Arrange
        var builder = new StateMachineBuilder<TestState, TestTrigger>(TestState.Initial);

        builder
            .ConfigureState(TestState.Initial)
            .Permit(TestTrigger.Start, TestState.Processing);

        // Act
        var result = builder.BuildSafe();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.State.Should().Be(TestState.Initial);
    }

    [Fact]
    public void BuildSafe_SecondCall_ShouldReturnAlreadyBuiltError()
    {
        // Arrange
        var builder = new StateMachineBuilder<TestState, TestTrigger>(TestState.Initial);

        builder
            .ConfigureState(TestState.Initial)
            .Permit(TestTrigger.Start, TestState.Processing);

        builder.BuildSafe(); // First call

        // Act
        var result = builder.BuildSafe(); // Second call

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Message.Should().Contain("already been built");
    }

    #endregion

    #region ConfigureState Tests

    [Fact]
    public void ConfigureState_ShouldReturnStateConfiguration()
    {
        // Arrange
        var builder = new StateMachineBuilder<TestState, TestTrigger>(TestState.Initial);

        // Act
        var config = builder.ConfigureState(TestState.Initial);

        // Assert
        config.Should().NotBeNull();
    }

    [Fact]
    public void ConfigureState_MultipleCalls_ShouldConfigureSameState()
    {
        // Arrange
        var builder = new StateMachineBuilder<TestState, TestTrigger>(TestState.Initial);

        // Act - Configure the same state multiple times with different transitions
        builder.ConfigureState(TestState.Initial)
            .Permit(TestTrigger.Start, TestState.Processing);

        builder.ConfigureState(TestState.Initial)
            .Permit(TestTrigger.Fail, TestState.Failed);

        var stateMachine = builder.Build();

        // Assert - Both transitions should be configured
        stateMachine.CanFire(TestTrigger.Start).Should().BeTrue();
        stateMachine.CanFire(TestTrigger.Fail).Should().BeTrue();
    }

    #endregion

    #region Permit Tests

    [Fact]
    public async Task Permit_BasicTransition_ShouldWork()
    {
        // Arrange
        var builder = new StateMachineBuilder<TestState, TestTrigger>(TestState.Initial);

        builder
            .ConfigureState(TestState.Initial)
            .Permit(TestTrigger.Start, TestState.Processing);

        var stateMachine = builder.Build();

        // Act
        await stateMachine.FireAsync(TestTrigger.Start);

        // Assert
        stateMachine.State.Should().Be(TestState.Processing);
    }

    [Fact]
    public async Task Permit_MultipleTransitions_ShouldWork()
    {
        // Arrange
        var builder = new StateMachineBuilder<TestState, TestTrigger>(TestState.Initial);

        builder
            .ConfigureState(TestState.Initial)
            .Permit(TestTrigger.Start, TestState.Processing);

        builder
            .ConfigureState(TestState.Processing)
            .Permit(TestTrigger.Complete, TestState.Completed)
            .Permit(TestTrigger.Fail, TestState.Failed);

        var stateMachine = builder.Build();

        // Act
        await stateMachine.FireAsync(TestTrigger.Start);
        await stateMachine.FireAsync(TestTrigger.Complete);

        // Assert
        stateMachine.State.Should().Be(TestState.Completed);
    }

    #endregion

    #region PermitReentry Tests

    [Fact]
    public async Task PermitReentry_ShouldAllowSelfTransition()
    {
        // Arrange
        var entryCount = 0;
        var builder = new StateMachineBuilder<TestState, TestTrigger>(TestState.Processing);

        builder
            .ConfigureState(TestState.Processing)
            .PermitReentry(TestTrigger.Reset)
            .OnEntry(() => entryCount++);

        var stateMachine = builder.Build();
        entryCount = 0; // Reset after initial build

        // Act
        await stateMachine.FireAsync(TestTrigger.Reset);

        // Assert
        stateMachine.State.Should().Be(TestState.Processing);
        entryCount.Should().Be(1);
    }

    #endregion

    #region Ignore Tests

    [Fact]
    public async Task Ignore_ShouldNotChangeState()
    {
        // Arrange
        var builder = new StateMachineBuilder<TestState, TestTrigger>(TestState.Initial);

        builder
            .ConfigureState(TestState.Initial)
            .Ignore(TestTrigger.Start);

        var stateMachine = builder.Build();

        // Act
        await stateMachine.FireAsync(TestTrigger.Start);

        // Assert
        stateMachine.State.Should().Be(TestState.Initial);
    }

    #endregion

    #region OnEntry/OnExit Tests

    [Fact]
    public async Task OnEntry_ShouldExecuteWhenEnteringState()
    {
        // Arrange
        var entryExecuted = false;
        var builder = new StateMachineBuilder<TestState, TestTrigger>(TestState.Initial);

        builder
            .ConfigureState(TestState.Initial)
            .Permit(TestTrigger.Start, TestState.Processing);

        builder
            .ConfigureState(TestState.Processing)
            .OnEntry(() => entryExecuted = true);

        var stateMachine = builder.Build();

        // Act
        await stateMachine.FireAsync(TestTrigger.Start);

        // Assert
        entryExecuted.Should().BeTrue();
    }

    [Fact]
    public async Task OnExit_ShouldExecuteWhenLeavingState()
    {
        // Arrange
        var exitExecuted = false;
        var builder = new StateMachineBuilder<TestState, TestTrigger>(TestState.Initial);

        builder
            .ConfigureState(TestState.Initial)
            .OnExit(() => exitExecuted = true)
            .Permit(TestTrigger.Start, TestState.Processing);

        var stateMachine = builder.Build();

        // Act
        await stateMachine.FireAsync(TestTrigger.Start);

        // Assert
        exitExecuted.Should().BeTrue();
    }

    [Fact]
    public async Task OnEntryAndExit_ShouldExecuteInCorrectOrder()
    {
        // Arrange
        var executionOrder = new List<string>();
        var builder = new StateMachineBuilder<TestState, TestTrigger>(TestState.Initial);

        builder
            .ConfigureState(TestState.Initial)
            .OnExit(() => executionOrder.Add("ExitInitial"))
            .Permit(TestTrigger.Start, TestState.Processing);

        builder
            .ConfigureState(TestState.Processing)
            .OnEntry(() => executionOrder.Add("EnterProcessing"));

        var stateMachine = builder.Build();

        // Act
        await stateMachine.FireAsync(TestTrigger.Start);

        // Assert
        executionOrder.Should().Equal("ExitInitial", "EnterProcessing");
    }

    #endregion

    #region PermitIf Tests

    [Theory]
    [InlineData(true, TestState.Processing)]
    [InlineData(false, TestState.Initial)]
    public async Task PermitIf_WithSingleGuard_ShouldRespectCondition(bool guardResult, TestState expectedState)
    {
        // Arrange
        var builder = new StateMachineBuilder<TestState, TestTrigger>(TestState.Initial);

        builder
            .ConfigureState(TestState.Initial)
            .PermitIf(TestTrigger.Start, TestState.Processing, () => guardResult);

        var stateMachine = builder.Build();

        // Act
        if (guardResult)
        {
            await stateMachine.FireAsync(TestTrigger.Start);
        }
        else
        {
            // When guard fails, CanFire should return false
            var canFire = stateMachine.CanFire(TestTrigger.Start);
            canFire.Should().BeFalse();
        }

        // Assert
        stateMachine.State.Should().Be(expectedState);
    }

    [Fact]
    public async Task PermitIf_WithMultipleConditions_ShouldRequireAllToPass()
    {
        // Arrange
        var guard1 = true;
        var guard2 = true;
        var builder = new StateMachineBuilder<TestState, TestTrigger>(TestState.Initial);

        builder
            .ConfigureState(TestState.Initial)
            .PermitIf(TestTrigger.Start, TestState.Processing, () => guard1 && guard2);

        var stateMachine = builder.Build();

        // Act
        await stateMachine.FireAsync(TestTrigger.Start);

        // Assert
        stateMachine.State.Should().Be(TestState.Processing);
    }

    #endregion

    #region Fluent Chaining Tests

    [Fact]
    public void FluentChaining_ComplexConfiguration_ShouldWork()
    {
        // Arrange & Act
        var builder = new StateMachineBuilder<TestState, TestTrigger>(TestState.Initial);

        builder.ConfigureState(TestState.Initial)
            .Permit(TestTrigger.Start, TestState.Processing);

        builder.ConfigureState(TestState.Processing)
            .Permit(TestTrigger.Complete, TestState.Completed)
            .Permit(TestTrigger.Fail, TestState.Failed)
            .OnEntry(() => { /* Processing logic */ });

        builder.ConfigureState(TestState.Completed)
            .Permit(TestTrigger.Reset, TestState.Initial);

        builder.ConfigureState(TestState.Failed)
            .Permit(TestTrigger.Reset, TestState.Initial);

        // Assert
        var result = builder.BuildSafe();
        result.IsSuccess.Should().BeTrue();
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task StateMachine_InvalidTrigger_ShouldThrow()
    {
        // Arrange
        var builder = new StateMachineBuilder<TestState, TestTrigger>(TestState.Initial);

        builder
            .ConfigureState(TestState.Initial)
            .Permit(TestTrigger.Start, TestState.Processing);

        var stateMachine = builder.Build();

        // Act
        Func<Task> act = async () => await stateMachine.FireAsync(TestTrigger.Complete); // Not permitted

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public void Build_WithoutConfiguration_ShouldCreateStateMachine()
    {
        // Arrange
        var builder = new StateMachineBuilder<TestState, TestTrigger>(TestState.Initial);

        // Act
        var stateMachine = builder.Build();

        // Assert
        stateMachine.Should().NotBeNull();
        stateMachine.State.Should().Be(TestState.Initial);
    }

    #endregion
}
