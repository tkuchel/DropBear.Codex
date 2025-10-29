using DropBear.Codex.Core.Enums;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Core.Results.Errors;
using DropBear.Codex.Core.Results.Extensions;
using FluentAssertions;

namespace DropBear.Codex.Core.Tests.Results;

/// <summary>
///     Tests for Result{T, TError} - operations with return values.
/// </summary>
public sealed class ResultWithValueTests
{
    #region Success Tests

    [Fact]
    public void Success_WithValue_ShouldCreateSuccessfulResultWithValue()
    {
        // Arrange
        const int expectedValue = 42;

        // Act
        var result = Result<int, SimpleError>.Success(expectedValue);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(expectedValue);
        result.Error.Should().BeNull();
        result.State.Should().Be(ResultState.Success);
    }

    [Fact]
    public void ValueOrDefault_OnSuccess_ShouldReturnValue()
    {
        // Arrange
        const string expectedValue = "test value";
        var result = Result<string, SimpleError>.Success(expectedValue);

        // Act
        var value = result.ValueOrDefault();

        // Assert
        value.Should().Be(expectedValue);
    }

    [Fact]
    public void ValueOrDefault_OnFailure_ShouldReturnDefault()
    {
        // Arrange
        var result = Result<int, SimpleError>.Failure(new SimpleError("Error"));

        // Act
        var value = result.ValueOrDefault();

        // Assert
        value.Should().Be(default(int));
    }

    [Fact]
    public void ValueOrDefault_OnFailure_WithCustomDefault_ShouldReturnCustomDefault()
    {
        // Arrange
        var result = Result<int, SimpleError>.Failure(new SimpleError("Error"));

        // Act
        var value = result.ValueOrDefault(99);

        // Assert
        value.Should().Be(99);
    }

    #endregion

    #region Failure Tests

    [Fact]
    public void Failure_ShouldCreateFailedResultWithoutValue()
    {
        // Arrange
        var error = new SimpleError("Operation failed");

        // Act
        var result = Result<string, SimpleError>.Failure(error);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(error);
        result.State.Should().Be(ResultState.Failure);
    }

    [Fact]
    public void ValueOrThrow_OnFailure_ShouldThrowException()
    {
        // Arrange
        var error = new SimpleError("Error occurred");
        var result = Result<int, SimpleError>.Failure(error);

        // Act & Assert
        var action = () => result.ValueOrThrow();
        action.Should().Throw<Exception>()
            .WithMessage("*Error occurred*");
    }

    [Fact]
    public void ValueOrThrow_OnSuccess_ShouldReturnValue()
    {
        // Arrange
        const int expectedValue = 42;
        var result = Result<int, SimpleError>.Success(expectedValue);

        // Act
        var value = result.ValueOrThrow();

        // Assert
        value.Should().Be(expectedValue);
    }

    #endregion

    #region Match Tests

    [Fact]
    public void Match_OnSuccess_ShouldReturnSuccessValue()
    {
        // Arrange
        var result = Result<int, SimpleError>.Success(42);

        // Act
        var output = result.Match(
            onSuccess: value => $"Success: {value}",
            onFailure: (error, ex) => $"Failed: {error.Message}");

        // Assert
        output.Should().Be("Success: 42");
    }

    [Fact]
    public void Match_OnFailure_ShouldReturnFailureValue()
    {
        // Arrange
        var result = Result<int, SimpleError>.Failure(new SimpleError("Something went wrong"));

        // Act
        var output = result.Match(
            onSuccess: value => $"Success: {value}",
            onFailure: (error, ex) => $"Failed: {error.Message}");

        // Assert
        output.Should().Be("Failed: Something went wrong");
    }

    [Fact]
    public void Match_Action_OnSuccess_ShouldExecuteSuccessAction()
    {
        // Arrange
        var result = Result<int, SimpleError>.Success(42);
        var capturedValue = 0;

        // Act
        result.Match(
            onSuccess: value => capturedValue = value,
            onFailure: (error, ex) => { });

        // Assert
        capturedValue.Should().Be(42);
    }

    #endregion

    #region Ensure Tests

    [Fact]
    public void Ensure_OnSuccessWithValidPredicate_ShouldReturnSuccess()
    {
        // Arrange
        var result = Result<int, SimpleError>.Success(10);
        var error = new SimpleError("Value too small");

        // Act
        var ensuredResult = result.Ensure(x => x > 5, error);

        // Assert
        ensuredResult.IsSuccess.Should().BeTrue();
        ensuredResult.Value.Should().Be(10);
    }

    [Fact]
    public void Ensure_OnSuccessWithInvalidPredicate_ShouldReturnFailure()
    {
        // Arrange
        var result = Result<int, SimpleError>.Success(3);
        var error = new SimpleError("Value too small");

        // Act
        var ensuredResult = result.Ensure(x => x > 5, error);

        // Assert
        ensuredResult.IsSuccess.Should().BeFalse();
        ensuredResult.Error.Should().Be(error);
    }

    [Fact]
    public void Ensure_OnFailure_ShouldReturnOriginalFailure()
    {
        // Arrange
        var originalError = new SimpleError("Original error");
        var result = Result<int, SimpleError>.Failure(originalError);
        var newError = new SimpleError("Validation error");

        // Act
        var ensuredResult = result.Ensure(x => x > 5, newError);

        // Assert
        ensuredResult.IsSuccess.Should().BeFalse();
        ensuredResult.Error.Should().Be(originalError);
    }

    #endregion

    #region Implicit Conversion Tests

    [Fact]
    public void ImplicitConversion_FromError_ShouldCreateFailureResult()
    {
        // Arrange
        var error = new SimpleError("Test error");

        // Act
        Result<int, SimpleError> result = error;

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(error);
    }

    #endregion

    #region Warning and PartialSuccess Tests

    [Fact]
    public void Warning_WithValue_ShouldCreateWarningResult()
    {
        // Arrange
        const int value = 42;
        var error = new SimpleError("Warning message");

        // Act
        var result = Result<int, SimpleError>.Warning(value, error);

        // Assert
        result.State.Should().Be(ResultState.Warning);
        result.Value.Should().Be(value);
        result.Error.Should().Be(error);
    }

    [Fact]
    public void PartialSuccess_WithValue_ShouldCreatePartialSuccessResult()
    {
        // Arrange
        const int value = 42;
        var error = new SimpleError("Partial completion");

        // Act
        var result = Result<int, SimpleError>.PartialSuccess(value, error);

        // Assert
        result.State.Should().Be(ResultState.PartialSuccess);
        result.Value.Should().Be(value);
        result.Error.Should().Be(error);
    }

    #endregion
}
