using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Errors;
using DropBear.Codex.Blazor.Models;
using DropBear.Codex.Core;
using DropBear.Codex.Core.Results.Base;
using FluentAssertions;

namespace DropBear.Codex.Blazor.Tests.Models;

public sealed class UploadResultTests
{
    #region Success Tests

    [Fact]
    public void Success_ShouldCreateSuccessfulResult()
    {
        // Act
        var result = UploadResult.Success();

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Status.Should().Be(UploadStatus.Success);
        result.Message.Should().Be("Upload completed successfully");
    }

    [Fact]
    public void Success_WithCustomMessage_ShouldUseDefaultMessage()
    {
        // Act
        var result = UploadResult.Success("Custom message");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Be("Upload completed successfully");
    }

    #endregion

    #region Failure Tests

    [Fact]
    public void Failure_WithMessage_ShouldCreateFailedResult()
    {
        // Arrange
        var message = "Upload failed";

        // Act
        var result = UploadResult.Failure(message);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.IsFailure.Should().BeTrue();
        result.Status.Should().Be(UploadStatus.Failure);
        result.Message.Should().Be(message);
    }

    [Fact]
    public void Failure_WithError_ShouldCreateFailedResult()
    {
        // Arrange
        var error = new FileUploadError("Upload failed for test.txt");

        // Act
        var result = UploadResult.Failure(error);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(UploadStatus.Failure);
        result.Message.Should().Contain("Upload failed");
    }

    #endregion

    #region Uploading Tests

    [Fact]
    public void Uploading_ShouldCreateUploadingResult()
    {
        // Act
        var result = UploadResult.Uploading();

        // Assert
        result.Status.Should().Be(UploadStatus.Uploading);
        result.IsSuccess.Should().BeTrue(); // Uploading is a transient success state
    }

    [Fact]
    public void Uploading_WithCustomMessage_ShouldUseDefaultMessage()
    {
        // Act
        var result = UploadResult.Uploading("Custom progress");

        // Assert
        result.Status.Should().Be(UploadStatus.Uploading);
        result.Message.Should().Be("Upload completed successfully");
    }

    #endregion

    #region Cancelled Tests

    [Fact]
    public void Cancelled_ShouldCreateCancelledResult()
    {
        // Arrange
        var fileName = "document.pdf";

        // Act
        var result = UploadResult.Cancelled(fileName);

        // Assert
        result.Status.Should().Be(UploadStatus.Cancelled);
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain(fileName);
        result.Message.Should().Contain("cancelled");
    }

    #endregion

    #region FromResult Tests

    [Fact]
    public void FromResult_WithSuccessResult_ShouldMapToSuccess()
    {
        // Arrange
        var coreResult = Result<Unit, FileUploadError>.Success(Unit.Value);

        // Act
        var result = UploadResult.FromResult(coreResult);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Status.Should().Be(UploadStatus.Success);
    }

    [Fact]
    public void FromResult_WithFailureResult_ShouldMapToFailure()
    {
        // Arrange
        var error = new FileUploadError("Test error");
        var coreResult = Result<Unit, FileUploadError>.Failure(error);

        // Act
        var result = UploadResult.FromResult(coreResult);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(UploadStatus.Failure);
        result.Message.Should().Be("Test error");
    }

    [Fact]
    public void FromResult_WithCancelledResult_ShouldMapToCancelled()
    {
        // Arrange
        var error = FileUploadError.Cancelled("test.pdf");
        var coreResult = Result<Unit, FileUploadError>.Cancelled(error);

        // Act
        var result = UploadResult.FromResult(coreResult);

        // Assert
        result.Status.Should().Be(UploadStatus.Cancelled);
    }

    [Fact]
    public void FromResult_WithWarningResult_ShouldMapToWarning()
    {
        // Arrange
        var error = new FileUploadError("Warning message");
        var coreResult = Result<Unit, FileUploadError>.Warning(Unit.Value, error);

        // Act
        var result = UploadResult.FromResult(coreResult);

        // Assert
        result.Status.Should().Be(UploadStatus.Warning);
    }

    #endregion

    #region Property Tests

    [Fact]
    public void Result_ShouldExposeUnderlyingResult()
    {
        // Arrange
        var uploadResult = UploadResult.Success();

        // Act
        var underlyingResult = uploadResult.Result;

        // Assert
        underlyingResult.Should().NotBeNull();
        underlyingResult.IsSuccess.Should().BeTrue();
    }

    #endregion
}
