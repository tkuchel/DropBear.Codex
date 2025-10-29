using DropBear.Codex.Core.Enums;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Core.Results.Errors;
using FluentAssertions;

namespace DropBear.Codex.Core.Tests.Results;

/// <summary>
///     Basic tests for Result{TError} - operations with no return value.
/// </summary>
public sealed class BasicResultTests
{
    #region Success Tests

    [Fact]
    public void Success_ShouldCreateSuccessfulResult()
    {
        // Act
        var result = Result<SimpleError>.Success();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Error.Should().BeNull();
        result.State.Should().Be(ResultState.Success);
    }

    #endregion

    #region Failure Tests

    [Fact]
    public void Failure_ShouldCreateFailedResult()
    {
        // Arrange
        var error = new SimpleError("Something went wrong");

        // Act
        var result = Result<SimpleError>.Failure(error);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(error);
        result.State.Should().Be(ResultState.Failure);
    }

    [Fact]
    public void Failure_WithException_ShouldPreserveException()
    {
        // Arrange
        var exception = new InvalidOperationException("Test exception");
        var error = new SimpleError("Error occurred");

        // Act
        var result = Result<SimpleError>.Failure(error, exception);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(error);
        result.Exception.Should().Be(exception);
    }

    #endregion

    #region Warning Tests

    [Fact]
    public void Warning_ShouldCreateWarningResult()
    {
        // Arrange
        var error = new SimpleError("This is a warning");

        // Act
        var result = Result<SimpleError>.Warning(error);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.State.Should().Be(ResultState.Warning);
        result.Error.Should().Be(error);
    }

    #endregion

    #region PartialSuccess Tests

    [Fact]
    public void PartialSuccess_ShouldCreatePartialSuccessResult()
    {
        // Arrange
        var error = new SimpleError("Partial completion");

        // Act
        var result = Result<SimpleError>.PartialSuccess(error);

        // Assert
        result.State.Should().Be(ResultState.PartialSuccess);
        result.Error.Should().Be(error);
    }

    #endregion

    #region Cancelled Tests

    [Fact]
    public void Cancelled_ShouldCreateCancelledResult()
    {
        // Arrange
        var error = new SimpleError("Operation cancelled");

        // Act
        var result = Result<SimpleError>.Cancelled(error);

        // Assert
        result.State.Should().Be(ResultState.Cancelled);
        result.Error.Should().Be(error);
    }

    #endregion

    #region Match Tests

    [Fact]
    public void Match_OnSuccess_ShouldExecuteSuccessFunc()
    {
        // Arrange
        var result = Result<SimpleError>.Success();

        // Act
        var output = result.Match(
            onSuccess: () => "Success",
            onFailure: (error, ex) => $"Failed: {error.Message}");

        // Assert
        output.Should().Be("Success");
    }

    [Fact]
    public void Match_OnFailure_ShouldExecuteFailureFunc()
    {
        // Arrange
        var error = new SimpleError("Something went wrong");
        var result = Result<SimpleError>.Failure(error);

        // Act
        var output = result.Match(
            onSuccess: () => "Success",
            onFailure: (err, ex) => $"Failed: {err.Message}");

        // Assert
        output.Should().Be("Failed: Something went wrong");
    }

    #endregion

    #region Tap Tests

    [Fact]
    public void Tap_OnSuccess_ShouldExecuteAction()
    {
        // Arrange
        var result = Result<SimpleError>.Success();
        var sideEffectExecuted = false;

        // Act
        var newResult = result.Tap(() => sideEffectExecuted = true);

        // Assert
        sideEffectExecuted.Should().BeTrue();
        newResult.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Tap_OnFailure_ShouldNotExecuteAction()
    {
        // Arrange
        var result = Result<SimpleError>.Failure(new SimpleError("Error"));
        var sideEffectExecuted = false;

        // Act
        var newResult = result.Tap(() => sideEffectExecuted = true);

        // Assert
        sideEffectExecuted.Should().BeFalse();
        newResult.IsSuccess.Should().BeFalse();
    }

    #endregion

    #region Async Tests

    [Fact]
    public async Task MatchAsync_OnSuccess_ShouldExecuteSuccessFunc()
    {
        // Arrange
        var result = Result<SimpleError>.Success();

        // Act
        var output = await result.MatchAsync(
            onSuccess: async () =>
            {
                await Task.Delay(10);
                return "Success";
            },
            onFailure: async (error, ex) =>
            {
                await Task.Delay(10);
                return $"Failed: {error.Message}";
            });

        // Assert
        output.Should().Be("Success");
    }

    [Fact]
    public async Task MatchAsync_OnFailure_ShouldExecuteFailureFunc()
    {
        // Arrange
        var error = new SimpleError("Something went wrong");
        var result = Result<SimpleError>.Failure(error);

        // Act
        var output = await result.MatchAsync(
            onSuccess: async () =>
            {
                await Task.Delay(10);
                return "Success";
            },
            onFailure: async (err, ex) =>
            {
                await Task.Delay(10);
                return $"Failed: {err.Message}";
            });

        // Assert
        output.Should().Be("Failed: Something went wrong");
    }

    #endregion

    #region Implicit Conversion Tests

    [Fact]
    public void ImplicitConversion_FromError_ShouldCreateFailureResult()
    {
        // Arrange
        var error = new SimpleError("Test error");

        // Act
        Result<SimpleError> result = error;

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(error);
    }

    #endregion
}
