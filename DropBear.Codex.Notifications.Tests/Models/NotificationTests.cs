using DropBear.Codex.Notifications.Enums;
using DropBear.Codex.Notifications.Models;
using FluentAssertions;

namespace DropBear.Codex.Notifications.Tests.Models;

public sealed class NotificationTests
{
    #region Construction Tests

    [Fact]
    public void Constructor_WithValidParameters_ShouldCreateNotification()
    {
        // Arrange
        var channelId = Guid.NewGuid();
        var message = "Test message";

        // Act
        var notification = new Notification(
            channelId,
            NotificationType.Toast,
            NotificationSeverity.Information,
            message);

        // Assert
        notification.Should().NotBeNull();
        notification.ChannelId.Should().Be(channelId);
        notification.Type.Should().Be(NotificationType.Toast);
        notification.Severity.Should().Be(NotificationSeverity.Information);
        notification.Message.Should().Be(message);
        notification.Title.Should().BeNull();
        notification.Data.Should().NotBeNull().And.BeEmpty();
        notification.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Constructor_WithEmptyChannelId_ShouldThrow()
    {
        // Arrange & Act
        Action act = () => new Notification(
            Guid.Empty,
            NotificationType.Toast,
            NotificationSeverity.Information,
            "Message");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithNullMessage_ShouldThrow()
    {
        // Arrange & Act
        Action act = () => new Notification(
            Guid.NewGuid(),
            NotificationType.Toast,
            NotificationSeverity.Information,
            null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithTitleAndData_ShouldSetProperties()
    {
        // Arrange
        var channelId = Guid.NewGuid();
        var title = "Test Title";
        var data = new Dictionary<string, object?> { ["key1"] = "value1", ["key2"] = 42 };

        // Act
        var notification = new Notification(
            channelId,
            NotificationType.PageAlert,
            NotificationSeverity.Warning,
            "Message",
            title,
            data);

        // Assert
        notification.Title.Should().Be(title);
        notification.Data.Should().HaveCount(2);
        notification.Data["key1"].Should().Be("value1");
        notification.Data["key2"].Should().Be(42);
    }

    #endregion

    #region WithUpdatedData Tests

    [Fact]
    public void WithUpdatedData_ShouldAddNewKey()
    {
        // Arrange
        var notification = CreateTestNotification();

        // Act
        var updated = notification.WithUpdatedData("newKey", "newValue");

        // Assert
        updated.Should().NotBeSameAs(notification); // New instance
        updated.Data.Should().ContainKey("newKey");
        updated.Data["newKey"].Should().Be("newValue");
    }

    [Fact]
    public void WithUpdatedData_ShouldUpdateExistingKey()
    {
        // Arrange
        var notification = CreateTestNotification()
            .WithUpdatedData("key1", "oldValue");

        // Act
        var updated = notification.WithUpdatedData("key1", "newValue");

        // Assert
        updated.Data["key1"].Should().Be("newValue");
    }

    [Fact]
    public void WithUpdatedData_WithNullKey_ShouldThrow()
    {
        // Arrange
        var notification = CreateTestNotification();

        // Act
        Action act = () => notification.WithUpdatedData(null!, "value");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void WithUpdatedData_WithWhitespaceKey_ShouldThrow()
    {
        // Arrange
        var notification = CreateTestNotification();

        // Act
        Action act = () => notification.WithUpdatedData("   ", "value");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region WithoutData Tests

    [Fact]
    public void WithoutData_ShouldRemoveKey()
    {
        // Arrange
        var notification = CreateTestNotification()
            .WithUpdatedData("key1", "value1")
            .WithUpdatedData("key2", "value2");

        // Act
        var updated = notification.WithoutData("key1");

        // Assert
        updated.Data.Should().NotContainKey("key1");
        updated.Data.Should().ContainKey("key2");
    }

    [Fact]
    public void WithoutData_WithNonExistentKey_ShouldNotThrow()
    {
        // Arrange
        var notification = CreateTestNotification();

        // Act
        var updated = notification.WithoutData("nonExistent");

        // Assert
        updated.Should().NotBeNull();
    }

    [Fact]
    public void WithoutData_WithNullKey_ShouldThrow()
    {
        // Arrange
        var notification = CreateTestNotification();

        // Act
        Action act = () => notification.WithoutData(null!);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region WithMessage Tests

    [Fact]
    public void WithMessage_ShouldUpdateMessage()
    {
        // Arrange
        var notification = CreateTestNotification();

        // Act
        var updated = notification.WithMessage("New Message");

        // Assert
        updated.Message.Should().Be("New Message");
        updated.Should().NotBeSameAs(notification);
    }

    [Fact]
    public void WithMessage_WithNull_ShouldThrow()
    {
        // Arrange
        var notification = CreateTestNotification();

        // Act
        Action act = () => notification.WithMessage(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region WithTitle Tests

    [Fact]
    public void WithTitle_ShouldUpdateTitle()
    {
        // Arrange
        var notification = CreateTestNotification();

        // Act
        var updated = notification.WithTitle("New Title");

        // Assert
        updated.Title.Should().Be("New Title");
        updated.Should().NotBeSameAs(notification);
    }

    [Fact]
    public void WithTitle_WithNull_ShouldSetNullTitle()
    {
        // Arrange
        var notification = CreateTestNotification().WithTitle("Title");

        // Act
        var updated = notification.WithTitle(null);

        // Assert
        updated.Title.Should().BeNull();
    }

    #endregion

    #region HasData Tests

    [Fact]
    public void HasData_WithExistingKey_ShouldReturnTrue()
    {
        // Arrange
        var notification = CreateTestNotification()
            .WithUpdatedData("key1", "value1");

        // Act
        var result = notification.HasData("key1");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void HasData_WithNonExistentKey_ShouldReturnFalse()
    {
        // Arrange
        var notification = CreateTestNotification();

        // Act
        var result = notification.HasData("nonExistent");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void HasData_WithNullKey_ShouldReturnFalse()
    {
        // Arrange
        var notification = CreateTestNotification();

        // Act
        var result = notification.HasData(null!);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region GetDataValue Tests

    [Fact]
    public void GetDataValue_WithExistingKey_ShouldReturnValue()
    {
        // Arrange
        var notification = CreateTestNotification()
            .WithUpdatedData("key1", "value1");

        // Act
        var result = notification.GetDataValue<string>("key1");

        // Assert
        result.Should().Be("value1");
    }

    [Fact]
    public void GetDataValue_WithNonExistentKey_ShouldReturnDefault()
    {
        // Arrange
        var notification = CreateTestNotification();

        // Act
        var result = notification.GetDataValue<string>("nonExistent");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetDataValue_WithDefaultValue_ShouldReturnDefault()
    {
        // Arrange
        var notification = CreateTestNotification();

        // Act
        var result = notification.GetDataValue("nonExistent", "default");

        // Assert
        result.Should().Be("default");
    }

    [Fact]
    public void GetDataValue_WithTypeConversion_ShouldConvert()
    {
        // Arrange
        var notification = CreateTestNotification()
            .WithUpdatedData("number", 42);

        // Act
        var result = notification.GetDataValue<int>("number");

        // Assert
        result.Should().Be(42);
    }

    #endregion

    #region Helper Methods

    private static Notification CreateTestNotification()
    {
        return new Notification(
            Guid.NewGuid(),
            NotificationType.Toast,
            NotificationSeverity.Information,
            "Test message");
    }

    #endregion
}
