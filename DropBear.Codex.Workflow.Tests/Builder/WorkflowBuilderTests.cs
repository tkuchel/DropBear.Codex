using DropBear.Codex.Workflow.Builder;
using DropBear.Codex.Workflow.Interfaces;
using DropBear.Codex.Workflow.Results;
using FluentAssertions;

namespace DropBear.Codex.Workflow.Tests.Builder;

public sealed class WorkflowBuilderTests
{
    #region Test Context

    private class TestContext
    {
        public int Value { get; set; }
        public List<string> ExecutedSteps { get; } = new();
    }

    #endregion

    #region Test Steps

    private class TestStep : IWorkflowStep<TestContext>
    {
        public string StepName => "TestStep";
        public bool CanRetry => true;
        public TimeSpan? Timeout => null;

        public ValueTask<StepResult> ExecuteAsync(TestContext context, CancellationToken cancellationToken = default)
        {
            context.ExecutedSteps.Add(StepName);
            context.Value++;
            return ValueTask.FromResult(StepResult.Success());
        }
    }

    private class SecondTestStep : IWorkflowStep<TestContext>
    {
        public string StepName => "SecondTestStep";
        public bool CanRetry => false;
        public TimeSpan? Timeout => TimeSpan.FromSeconds(10);

        public ValueTask<StepResult> ExecuteAsync(TestContext context, CancellationToken cancellationToken = default)
        {
            context.ExecutedSteps.Add(StepName);
            context.Value *= 2;
            return ValueTask.FromResult(StepResult.Success());
        }
    }

    private class FailingStep : IWorkflowStep<TestContext>
    {
        public string StepName => "FailingStep";
        public bool CanRetry => true;
        public TimeSpan? Timeout => null;

        public ValueTask<StepResult> ExecuteAsync(TestContext context, CancellationToken cancellationToken = default)
        {
            context.ExecutedSteps.Add(StepName);
            return ValueTask.FromResult(StepResult.Failure("Step failed intentionally"));
        }
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidParameters_ShouldCreateBuilder()
    {
        // Act
        var builder = new WorkflowBuilder<TestContext>("test-workflow", "Test Workflow");

        // Assert
        builder.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullWorkflowId_ShouldThrow()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            new WorkflowBuilder<TestContext>(null!, "Display Name"));
    }

    [Fact]
    public void Constructor_WithWhitespaceWorkflowId_ShouldThrow()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            new WorkflowBuilder<TestContext>("   ", "Display Name"));
    }

    [Fact]
    public void Constructor_WithNullDisplayName_ShouldThrow()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            new WorkflowBuilder<TestContext>("workflow-id", null!));
    }

    [Fact]
    public void Constructor_WithInvalidWorkflowId_ShouldThrow()
    {
        // Workflow ID must be alphanumeric, hyphens, underscores, and periods only
        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            new WorkflowBuilder<TestContext>("workflow id with spaces", "Display Name"));
    }

    [Fact]
    public void Constructor_WithTooLongWorkflowId_ShouldThrow()
    {
        // Arrange
        var longId = new string('a', 300); // Exceeds max length

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            new WorkflowBuilder<TestContext>(longId, "Display Name"));
    }

    #endregion

    #region WithTimeout Tests

    [Fact]
    public void WithTimeout_WithValidTimeout_ShouldSetTimeout()
    {
        // Arrange
        var builder = new WorkflowBuilder<TestContext>("test-workflow", "Test Workflow");

        // Act
        var result = builder.WithTimeout(TimeSpan.FromMinutes(5));

        // Assert
        result.Should().BeSameAs(builder); // Fluent interface
    }

    [Fact]
    public void WithTimeout_WithTooShortTimeout_ShouldThrow()
    {
        // Arrange
        var builder = new WorkflowBuilder<TestContext>("test-workflow", "Test Workflow");

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            builder.WithTimeout(TimeSpan.FromMilliseconds(100)));
    }

    [Fact]
    public void WithTimeout_WithTooLongTimeout_ShouldThrow()
    {
        // Arrange
        var builder = new WorkflowBuilder<TestContext>("test-workflow", "Test Workflow");

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            builder.WithTimeout(TimeSpan.FromDays(100)));
    }

    #endregion

    #region StartWith Tests

    [Fact]
    public void StartWith_ShouldSetRootStep()
    {
        // Arrange
        var builder = new WorkflowBuilder<TestContext>("test-workflow", "Test Workflow");

        // Act
        var result = builder.StartWith<TestStep>();

        // Assert
        result.Should().BeSameAs(builder);
    }

    [Fact]
    public void StartWith_CalledTwice_ShouldThrow()
    {
        // Arrange
        var builder = new WorkflowBuilder<TestContext>("test-workflow", "Test Workflow");
        builder.StartWith<TestStep>();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => builder.StartWith<SecondTestStep>());
    }

    #endregion

    #region Then Tests

    [Fact]
    public void Then_AfterStartWith_ShouldChainStep()
    {
        // Arrange
        var builder = new WorkflowBuilder<TestContext>("test-workflow", "Test Workflow");

        // Act
        var result = builder
            .StartWith<TestStep>()
            .Then<SecondTestStep>();

        // Assert
        result.Should().BeSameAs(builder);
    }

    [Fact]
    public void Then_WithoutStartWith_ShouldThrow()
    {
        // Arrange
        var builder = new WorkflowBuilder<TestContext>("test-workflow", "Test Workflow");

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => builder.Then<TestStep>());
    }

    #endregion

    #region Build Tests

    [Fact]
    public void Build_WithValidConfiguration_ShouldReturnWorkflowDefinition()
    {
        // Arrange
        var builder = new WorkflowBuilder<TestContext>("test-workflow", "Test Workflow");
        builder.StartWith<TestStep>();

        // Act
        var workflow = builder.Build();

        // Assert
        workflow.Should().NotBeNull();
    }

    [Fact]
    public void Build_WithoutRootNode_ShouldThrow()
    {
        // Arrange
        var builder = new WorkflowBuilder<TestContext>("test-workflow", "Test Workflow");

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void Build_WithMultipleSteps_ShouldCreateValidWorkflow()
    {
        // Arrange
        var builder = new WorkflowBuilder<TestContext>("test-workflow", "Test Workflow");

        // Act
        var workflow = builder
            .StartWith<TestStep>()
            .Then<SecondTestStep>()
            .Build();

        // Assert
        workflow.Should().NotBeNull();
    }

    [Fact]
    public void Build_WithTimeout_ShouldCreateWorkflowWithTimeout()
    {
        // Arrange
        var builder = new WorkflowBuilder<TestContext>("test-workflow", "Test Workflow");

        // Act
        var workflow = builder
            .WithTimeout(TimeSpan.FromMinutes(10))
            .StartWith<TestStep>()
            .Build();

        // Assert
        workflow.Should().NotBeNull();
    }

    #endregion
}
