using DropBear.Codex.Notifications.Errors;
using FluentAssertions;

namespace DropBear.Codex.Notifications.Tests.Errors;

public sealed class NotificationErrorTests
{
    #region Factory Method Tests

    [Fact]
    public void NotFound_ShouldCreateError()
    {
        // Arrange
        var notificationId = Guid.NewGuid();

        // Act
        var error = NotificationError.NotFound(notificationId);

        // Assert
        error.Should().NotBeNull();
        error.Message.Should().Contain(notificationId.ToString());
    }

    [Fact]
    public void PublishFailed_ShouldCreateTransientError()
    {
        // Arrange
        var reason = "Network timeout";

        // Act
        var error = NotificationError.PublishFailed(reason);

        // Assert
        error.Should().NotBeNull();
        error.Message.Should().Contain(reason);
        error.IsTransient.Should().BeTrue();
    }

    [Fact]
    public void EncryptionFailed_ShouldCreateError()
    {
        // Arrange
        var reason = "Invalid key";

        // Act
        var error = NotificationError.EncryptionFailed(reason);

        // Assert
        error.Should().NotBeNull();
        error.Message.Should().Contain(reason);
        error.Message.Should().Contain("Failed to encrypt notification");
    }

    [Fact]
    public void DecryptionFailed_ShouldCreateError()
    {
        // Arrange
        var reason = "Corrupted data";

        // Act
        var error = NotificationError.DecryptionFailed(reason);

        // Assert
        error.Should().NotBeNull();
        error.Message.Should().Contain(reason);
        error.Message.Should().Contain("Failed to decrypt notification");
    }

    [Fact]
    public void DatabaseOperationFailed_ShouldCreateTransientError()
    {
        // Arrange
        var operation = "Save";
        var reason = "Connection lost";

        // Act
        var error = NotificationError.DatabaseOperationFailed(operation, reason);

        // Assert
        error.Should().NotBeNull();
        error.Message.Should().Contain(operation);
        error.Message.Should().Contain(reason);
        error.IsTransient.Should().BeTrue();
    }

    [Fact]
    public void InvalidData_ShouldCreateError()
    {
        // Arrange
        var field = "Message";
        var reason = "Cannot be empty";

        // Act
        var error = NotificationError.InvalidData(field, reason);

        // Assert
        error.Should().NotBeNull();
        error.Message.Should().Contain(field);
        error.Message.Should().Contain(reason);
    }

    [Fact]
    public void FromException_WithNonTransient_ShouldCreateError()
    {
        // Arrange
        var exception = new InvalidOperationException("Test error");

        // Act
        var error = NotificationError.FromException(exception, isTransient: false);

        // Assert
        error.Should().NotBeNull();
        error.Message.Should().Contain("Test error");
        error.IsTransient.Should().BeFalse();
    }

    [Fact]
    public void FromException_WithTransient_ShouldCreateTransientError()
    {
        // Arrange
        var exception = new TimeoutException("Request timed out");

        // Act
        var error = NotificationError.FromException(exception, isTransient: true);

        // Assert
        error.Should().NotBeNull();
        error.Message.Should().Contain("Request timed out");
        error.IsTransient.Should().BeTrue();
    }

    #endregion
}
