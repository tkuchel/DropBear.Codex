using DropBear.Codex.Core.Enums;
using DropBear.Codex.Core.Results.Errors;
using FluentAssertions;

namespace DropBear.Codex.Core.Tests.Results;

/// <summary>
///     Tests for ResultError hierarchy (SimpleError, CodedError, OperationError).
/// </summary>
public sealed class ResultErrorTests
{
    #region SimpleError Tests

    [Fact]
    public void SimpleError_WithMessage_ShouldCreateError()
    {
        // Arrange
        const string message = "An error occurred";

        // Act
        var error = new SimpleError(message);

        // Assert
        error.Message.Should().Be(message);
        error.Severity.Should().Be(ErrorSeverity.Medium);
        error.Category.Should().Be(ErrorCategory.General);
    }

    [Fact]
    public void SimpleError_Create_ShouldCreateError()
    {
        // Arrange
        const string message = "Test error";

        // Act
        var error = SimpleError.Create(message);

        // Assert
        error.Message.Should().Be(message);
    }

    [Fact]
    public void SimpleError_FromException_ShouldCreateErrorWithMetadata()
    {
        // Arrange
        var exception = new InvalidOperationException("Test exception");

        // Act
        var error = SimpleError.FromException(exception);

        // Assert
        error.Message.Should().Be("Test exception");
        error.Metadata.Should().ContainKey("ExceptionType");
        error.Metadata["ExceptionType"].Should().Be("InvalidOperationException");
    }

    [Fact]
    public void SimpleError_WithSeverity_ShouldSetSeverity()
    {
        // Arrange
        var error = new SimpleError("Critical error");

        // Act
        var updatedError = error.WithSeverity(ErrorSeverity.Critical);

        // Assert
        updatedError.Severity.Should().Be(ErrorSeverity.Critical);
        updatedError.Message.Should().Be("Critical error");
    }

    [Fact]
    public void SimpleError_WithCategory_ShouldSetCategory()
    {
        // Arrange
        var error = new SimpleError("Validation error");

        // Act
        var updatedError = error.WithCategory(ErrorCategory.Validation);

        // Assert
        updatedError.Category.Should().Be(ErrorCategory.Validation);
    }

    [Fact]
    public void SimpleError_WithMetadata_ShouldAddMetadata()
    {
        // Arrange
        var error = new SimpleError("Error with context");

        // Act
        var updatedError = error
            .WithMetadata("UserId", 123)
            .WithMetadata("Timestamp", DateTime.UtcNow);

        // Assert
        updatedError.Metadata.Should().ContainKey("UserId");
        updatedError.Metadata.Should().ContainKey("Timestamp");
        updatedError.Metadata["UserId"].Should().Be(123);
    }

    #endregion

    #region CodedError Tests

    [Fact]
    public void CodedError_WithCode_ShouldCreateError()
    {
        // Arrange
        const string code = "ERR_001";
        const string message = "Database connection failed";

        // Act
        var error = new CodedError(message, code);

        // Assert
        error.Code.Should().Be(code);
        error.Message.Should().Be(message);
    }

    [Fact]
    public void CodedError_WithCode_ShouldSetCode()
    {
        // Arrange
        var error = new CodedError("Original error", "ERR_001");

        // Act
        var updatedError = error.WithCode("ERR_002");

        // Assert
        updatedError.Code.Should().Be("ERR_002");
        updatedError.Message.Should().Be("Original error");
    }

    [Fact]
    public void CodedError_FluentConfiguration_ShouldApplyAllSettings()
    {
        // Act
        var error = new CodedError("Authentication failed", "AUTH_FAILED")
            .WithSeverity(ErrorSeverity.High)
            .WithCategory(ErrorCategory.Authorization)
            .WithMetadata("Attempt", 3);

        // Assert
        error.Code.Should().Be("AUTH_FAILED");
        error.Severity.Should().Be(ErrorSeverity.High);
        error.Category.Should().Be(ErrorCategory.Authorization);
        error.Metadata.Should().ContainKey("Attempt");
    }

    #endregion

    #region OperationError Tests

    [Fact]
    public void OperationError_WithOperation_ShouldCreateError()
    {
        // Arrange
        const string operation = "UserRegistration";
        const string message = "User already exists";

        // Act
        var error = OperationError.ForOperation(operation, message);

        // Assert
        error.OperationName.Should().Be(operation);
        error.Message.Should().Be(message);
    }

    [Fact]
    public void OperationError_FluentConfiguration_ShouldApplyAllSettings()
    {
        // Arrange
        var baseError = OperationError.ForOperation("CreateOrder", "Order creation failed");

        // Act
        var error = baseError
            .WithSeverity(ErrorSeverity.High)
            .WithCategory(ErrorCategory.General)
            .WithMetadata("ProductId", "PROD_789");

        // Assert
        baseError.OperationName.Should().Be("CreateOrder");
        error.Severity.Should().Be(ErrorSeverity.High);
        error.Category.Should().Be(ErrorCategory.General);
        error.Metadata.Should().ContainKey("ProductId");
    }

    [Fact]
    public void OperationError_ToString_ShouldIncludeOperationName()
    {
        // Arrange
        var error = OperationError.ForOperation("TestOperation", "Operation failed");

        // Act
        var result = error.ToString();

        // Assert
        result.Should().Contain("TestOperation");
        result.Should().Contain("Operation failed");
    }

    #endregion

    #region ErrorSeverity Tests

    [Theory]
    [InlineData(ErrorSeverity.Info)]
    [InlineData(ErrorSeverity.Low)]
    [InlineData(ErrorSeverity.Medium)]
    [InlineData(ErrorSeverity.High)]
    [InlineData(ErrorSeverity.Critical)]
    public void ErrorSeverity_AllValues_ShouldBeValid(ErrorSeverity severity)
    {
        // Arrange
        var error = new SimpleError("Test error");

        // Act
        var updatedError = error.WithSeverity(severity);

        // Assert
        updatedError.Severity.Should().Be(severity);
    }

    #endregion

    #region ErrorCategory Tests

    [Theory]
    [InlineData(ErrorCategory.Unknown)]
    [InlineData(ErrorCategory.General)]
    [InlineData(ErrorCategory.Validation)]
    [InlineData(ErrorCategory.Authorization)]
    [InlineData(ErrorCategory.Technical)]
    [InlineData(ErrorCategory.Timeout)]
    [InlineData(ErrorCategory.Cancelled)]
    [InlineData(ErrorCategory.InvalidOperation)]
    [InlineData(ErrorCategory.IO)]
    [InlineData(ErrorCategory.Network)]
    [InlineData(ErrorCategory.Critical)]
    public void ErrorCategory_AllValues_ShouldBeValid(ErrorCategory category)
    {
        // Arrange
        var error = new SimpleError("Test error");

        // Act
        var updatedError = error.WithCategory(category);

        // Assert
        updatedError.Category.Should().Be(category);
    }

    #endregion

    #region Immutability Tests

    [Fact]
    public void ResultError_Modifications_ShouldCreateNewInstances()
    {
        // Arrange
        var originalError = new SimpleError("Original message");

        // Act
        var modifiedError = originalError
            .WithSeverity(ErrorSeverity.Critical)
            .WithMetadata("Key", "Value");

        // Assert
        originalError.Severity.Should().Be(ErrorSeverity.Medium); // Unchanged
        originalError.Metadata.Should().BeEmpty(); // Unchanged
        modifiedError.Severity.Should().Be(ErrorSeverity.Critical);
        modifiedError.Metadata.Should().ContainKey("Key");
    }

    [Fact]
    public void CodedError_CodeModification_ShouldCreateNewInstance()
    {
        // Arrange
        var originalError = new CodedError("Original", "CODE_001");

        // Act
        var modifiedError = originalError.WithCode("CODE_002");

        // Assert
        originalError.Code.Should().Be("CODE_001");
        modifiedError.Code.Should().Be("CODE_002");
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void SimpleError_ToString_ShouldIncludeMessage()
    {
        // Arrange
        var error = new SimpleError("Test error message");

        // Act
        var result = error.ToString();

        // Assert
        result.Should().Contain("Test error message");
    }

    [Fact]
    public void CodedError_ToString_ShouldIncludeCodeAndMessage()
    {
        // Arrange
        var error = new CodedError("Test error", "ERR_123");

        // Act
        var result = error.ToString();

        // Assert
        result.Should().Contain("ERR_123");
        result.Should().Contain("Test error");
    }

    #endregion
}
