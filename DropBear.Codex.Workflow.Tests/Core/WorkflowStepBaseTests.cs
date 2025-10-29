using DropBear.Codex.Workflow.Core;
using DropBear.Codex.Workflow.Results;
using FluentAssertions;

namespace DropBear.Codex.Workflow.Tests.Core;

public sealed class WorkflowStepBaseTests
{
    #region Test Context

    private class TestContext
    {
        public int Value { get; set; }
        public bool Executed { get; set; }
    }

    #endregion

    #region Test Steps

    private class SuccessfulStep : WorkflowStepBase<TestContext>
    {
        public override string StepName => "SuccessfulStep";
        public override bool CanRetry => true;
        public override TimeSpan? Timeout => null;

        public override ValueTask<StepResult> ExecuteAsync(TestContext context, CancellationToken cancellationToken = default)
        {
            context.Value = 42;
            context.Executed = true;
            return ValueTask.FromResult(Success());
        }
    }

    private class FailingStep : WorkflowStepBase<TestContext>
    {
        public override string StepName => "FailingStep";
        public override bool CanRetry => false;
        public override TimeSpan? Timeout => TimeSpan.FromSeconds(5);

        public override ValueTask<StepResult> ExecuteAsync(TestContext context, CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(Failure("Step failed"));
        }
    }

    private class ExceptionThrowingStep : WorkflowStepBase<TestContext>
    {
        public override string StepName => "ExceptionThrowingStep";

        public override ValueTask<StepResult> ExecuteAsync(TestContext context, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Test exception");
        }
    }

    private class StepWithCompensation : WorkflowStepBase<TestContext>
    {
        public override string StepName => "StepWithCompensation";

        public override ValueTask<StepResult> ExecuteAsync(TestContext context, CancellationToken cancellationToken = default)
        {
            context.Value = 100;
            return ValueTask.FromResult(Success());
        }

        public override ValueTask<StepResult> CompensateAsync(TestContext context, CancellationToken cancellationToken = default)
        {
            context.Value = 0;
            return ValueTask.FromResult(Success());
        }
    }

    #endregion

    #region Execution Tests

    [Fact]
    public async Task ExecuteAsync_WithSuccessfulStep_ShouldReturnSuccess()
    {
        // Arrange
        var step = new SuccessfulStep();
        var context = new TestContext();

        // Act
        var result = await step.ExecuteAsync(context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        context.Value.Should().Be(42);
        context.Executed.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WithFailingStep_ShouldReturnFailure()
    {
        // Arrange
        var step = new FailingStep();
        var context = new TestContext();

        // Act
        var result = await step.ExecuteAsync(context);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Message.Should().Contain("Step failed");
    }

    [Fact]
    public async Task ExecuteAsync_WithException_ShouldThrow()
    {
        // Arrange
        var step = new ExceptionThrowingStep();
        var context = new TestContext();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await step.ExecuteAsync(context));
    }

    #endregion

    #region Compensation Tests

    [Fact]
    public async Task CompensateAsync_WithDefaultImplementation_ShouldReturnSuccess()
    {
        // Arrange
        var step = new SuccessfulStep();
        var context = new TestContext();

        // Act
        var result = await step.CompensateAsync(context);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task CompensateAsync_WithCustomImplementation_ShouldExecuteCompensation()
    {
        // Arrange
        var step = new StepWithCompensation();
        var context = new TestContext();

        // Execute first
        await step.ExecuteAsync(context);
        context.Value.Should().Be(100);

        // Act - compensate
        var result = await step.CompensateAsync(context);

        // Assert
        result.IsSuccess.Should().BeTrue();
        context.Value.Should().Be(0);
    }

    #endregion

    #region Helper Method Tests

    [Fact]
    public void Success_ShouldReturnSuccessResult()
    {
        // Arrange
        var step = new SuccessfulStep();

        // Act - accessing protected method via execution
        var result = StepResult.Success();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.ShouldRetry.Should().BeFalse();
    }

    [Fact]
    public void Failure_WithMessage_ShouldReturnFailureResult()
    {
        // Arrange & Act
        var result = StepResult.Failure("Test error");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Message.Should().Contain("Test error");
    }

    [Fact]
    public void Failure_WithException_ShouldPreserveException()
    {
        // Arrange
        var exception = new InvalidOperationException("Test");

        // Act
        var result = StepResult.Failure(exception);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.SourceException.Should().Be(exception);
    }

    #endregion

    #region Property Tests

    [Fact]
    public void StepName_ShouldReturnCorrectName()
    {
        // Arrange
        var step = new SuccessfulStep();

        // Act & Assert
        step.StepName.Should().Be("SuccessfulStep");
    }

    [Fact]
    public void CanRetry_ShouldReturnCorrectValue()
    {
        // Arrange
        var retryableStep = new SuccessfulStep();
        var nonRetryableStep = new FailingStep();

        // Act & Assert
        retryableStep.CanRetry.Should().BeTrue();
        nonRetryableStep.CanRetry.Should().BeFalse();
    }

    [Fact]
    public void Timeout_ShouldReturnCorrectValue()
    {
        // Arrange
        var stepWithTimeout = new FailingStep();
        var stepWithoutTimeout = new SuccessfulStep();

        // Act & Assert
        stepWithTimeout.Timeout.Should().Be(TimeSpan.FromSeconds(5));
        stepWithoutTimeout.Timeout.Should().BeNull();
    }

    #endregion
}
