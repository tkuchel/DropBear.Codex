using System.Reflection;
using DropBear.Codex.Workflow.Nodes;
using DropBear.Codex.Workflow.Persistence.Steps;
using DropBear.Codex.Workflow.Results;
using FluentAssertions;

namespace DropBear.Codex.Workflow.Tests.Core;

public sealed class StepNodeSignalHandlingTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldRoutePendingSignalPayloadToProcessSignalAsync()
    {
        var context = new SignalContext();
        var payload = new ApprovalSignal { ApprovedBy = "codex" };
        var node = new StepNode<SignalContext, ApprovalStep>();
        var services = new TestServiceProvider(new ApprovalStep());

        using var scope = BeginSignalScope("approval", payload);

        var result = await node.ExecuteAsync(context, services);

        result.StepResult.IsSuccess.Should().BeTrue();
        context.ProcessedBySignal.Should().BeTrue();
        context.ApprovedBy.Should().Be("codex");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldFailWhenSignalPayloadTypeDoesNotMatchStepContract()
    {
        var context = new SignalContext();
        var node = new StepNode<SignalContext, ApprovalStep>();
        var services = new TestServiceProvider(new ApprovalStep());

        using var scope = BeginSignalScope("approval", 123);

        var result = await node.ExecuteAsync(context, services);

        result.StepResult.IsSuccess.Should().BeFalse();
        result.StepResult.Error!.Message.Should().Contain("payload type mismatch");
        context.ProcessedBySignal.Should().BeFalse();
    }

    private static IDisposable BeginSignalScope(string signalName, object? payload)
    {
        var accessorType = typeof(StepNode<,>).Assembly
            .GetType("DropBear.Codex.Workflow.Persistence.Implementation.WorkflowSignalContextAccessor");
        accessorType.Should().NotBeNull();

        var beginScopeMethod = accessorType!.GetMethod("BeginScope", BindingFlags.Public | BindingFlags.Static);
        beginScopeMethod.Should().NotBeNull();

        var scope = beginScopeMethod!.Invoke(null, [signalName, payload]);
        scope.Should().BeAssignableTo<IDisposable>();
        return (IDisposable)scope!;
    }

    private sealed class SignalContext
    {
        public bool ProcessedBySignal { get; set; }
        public string? ApprovedBy { get; set; }
    }

    private sealed class ApprovalSignal
    {
        public string ApprovedBy { get; init; } = string.Empty;
    }

    private sealed class ApprovalStep : WaitForSignalStep<SignalContext, ApprovalSignal>
    {
        public override string StepName => "ApprovalStep";
        public override string SignalName => "approval";

        public override ValueTask<StepResult> ProcessSignalAsync(
            SignalContext context,
            ApprovalSignal? signalData,
            CancellationToken cancellationToken = default)
        {
            context.ProcessedBySignal = true;
            context.ApprovedBy = signalData?.ApprovedBy;
            return ValueTask.FromResult(Success());
        }
    }

    private sealed class TestServiceProvider(ApprovalStep step) : IServiceProvider
    {
        public object? GetService(Type serviceType)
        {
            return serviceType == typeof(ApprovalStep) ? step : null;
        }
    }
}
