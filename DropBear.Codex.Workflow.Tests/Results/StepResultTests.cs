using DropBear.Codex.Core.Enums;
using DropBear.Codex.Workflow.Results;
using FluentAssertions;

namespace DropBear.Codex.Workflow.Tests.Results;

public sealed class StepResultTests
{
    #region Success Tests

    [Fact]
    public void Success_ShouldCreateSuccessfulResult()
    {
        // Act
        var result = StepResult.Success();

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.ShouldRetry.Should().BeFalse();
        result.Metadata.Should().BeNull();
        result.Error.Should().BeNull();
    }

    [Fact]
    public void Success_WithMetadata_ShouldIncludeMetadata()
    {
        // Arrange
        var metadata = new Dictionary<string, object> { ["key"] = "value" };

        // Act
        var result = StepResult.Success(metadata);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Metadata.Should().NotBeNull();
        result.Metadata.Should().ContainKey("key");
        result.Metadata!["key"].Should().Be("value");
    }

    #endregion

    #region Failure Tests

    [Fact]
    public void Failure_WithMessage_ShouldCreateFailedResult()
    {
        // Arrange
        var errorMessage = "Step failed";

        // Act
        var result = StepResult.Failure(errorMessage);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Message.Should().Contain(errorMessage);
        result.ShouldRetry.Should().BeFalse();
    }

    [Fact]
    public void Failure_WithMessageAndRetry_ShouldSetRetryFlag()
    {
        // Act
        var result = StepResult.Failure("Error", shouldRetry: true);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ShouldRetry.Should().BeTrue();
    }

    [Fact]
    public void Failure_WithNullOrWhitespaceMessage_ShouldThrow()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => StepResult.Failure(string.Empty));
        Assert.Throws<ArgumentException>(() => StepResult.Failure("   "));
    }

    [Fact]
    public void Failure_WithException_ShouldPreserveException()
    {
        // Arrange
        var exception = new InvalidOperationException("Test error");

        // Act
        var result = StepResult.Failure(exception);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.SourceException.Should().Be(exception);
        result.Error.Message.Should().Contain("Test error");
    }

    [Fact]
    public void Failure_WithNullException_ShouldThrow()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => StepResult.Failure((Exception)null!));
    }

    [Fact]
    public void Failure_WithMetadata_ShouldIncludeMetadata()
    {
        // Arrange
        var metadata = new Dictionary<string, object> { ["attempt"] = 3 };

        // Act
        var result = StepResult.Failure("Error", metadata: metadata);

        // Assert
        result.Metadata.Should().NotBeNull();
        result.Metadata!["attempt"].Should().Be(3);
    }

    #endregion

    #region FromError Tests

    [Fact]
    public void FromError_ShouldCreateResultFromError()
    {
        // Arrange
        var error = new DropBear.Codex.Core.Results.Errors.SimpleError("Custom error");

        // Act
        var result = StepResult.FromError(error);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(error);
        result.ShouldRetry.Should().BeFalse();
    }

    [Fact]
    public void FromError_WithRetry_ShouldSetRetryFlag()
    {
        // Arrange
        var error = new DropBear.Codex.Core.Results.Errors.SimpleError("Error");

        // Act
        var result = StepResult.FromError(error, shouldRetry: true);

        // Assert
        result.ShouldRetry.Should().BeTrue();
    }

    [Fact]
    public void FromError_WithNullError_ShouldThrow()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => StepResult.FromError(null!));
    }

    #endregion

    #region Suspend Tests

    [Fact]
    public void Suspend_ShouldCreateSuspensionResult()
    {
        // Arrange
        var signalName = "WaitForApproval";

        // Act
        var result = StepResult.Suspend(signalName);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Message.Should().Contain(signalName);
        result.ShouldRetry.Should().BeFalse();
        result.Metadata.Should().NotBeNull();
        result.Metadata.Should().ContainKey("IsSuspension");
        result.Metadata!["IsSuspension"].Should().Be(true);
        result.Metadata.Should().ContainKey("SignalName");
        result.Metadata["SignalName"].Should().Be(signalName);
    }

    [Fact]
    public void Suspend_WithMetadata_ShouldIncludeAllMetadata()
    {
        // Arrange
        var metadata = new Dictionary<string, object> { ["userId"] = "123" };

        // Act
        var result = StepResult.Suspend("WaitForApproval", metadata);

        // Assert
        result.Metadata.Should().ContainKey("userId");
        result.Metadata!["userId"].Should().Be("123");
        result.Metadata.Should().ContainKey("IsSuspension");
    }

    [Fact]
    public void Suspend_WithNullOrWhitespaceSignalName_ShouldThrow()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => StepResult.Suspend(string.Empty));
        Assert.Throws<ArgumentException>(() => StepResult.Suspend("   "));
    }

    [Fact]
    public void Suspend_WithTooLongSignalName_ShouldThrow()
    {
        // Arrange
        var longSignal = new string('a', 300); // Exceeds max length

        // Act & Assert
        Assert.Throws<ArgumentException>(() => StepResult.Suspend(longSignal));
    }

    #endregion
}
