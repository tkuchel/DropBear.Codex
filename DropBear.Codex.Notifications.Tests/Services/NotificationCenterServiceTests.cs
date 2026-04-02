using DropBear.Codex.Core.Results;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Notifications.Entities;
using DropBear.Codex.Notifications.Errors;
using DropBear.Codex.Notifications.Interfaces;
using DropBear.Codex.Notifications.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DropBear.Codex.Notifications.Tests.Services;

public sealed class NotificationCenterServiceTests
{
    private readonly Mock<INotificationRepository> _repository = new();

    [Fact]
    public async Task GetNotificationAsync_ShouldScopeLookupToUser()
    {
        var userId = Guid.NewGuid();
        var notificationId = Guid.NewGuid();
        var notification = new NotificationRecord { Id = notificationId, UserId = userId, Message = "Scoped" };
        _repository
            .Setup(repo => repo.GetByIdAsync(userId, notificationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<NotificationRecord?, NotificationError>.Success(notification));

        using var service = CreateService();

        var result = await service.GetNotificationAsync(userId, notificationId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(notification);
        _repository.Verify(repo => repo.GetByIdAsync(userId, notificationId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MarkAsReadAsync_ShouldForwardUserIdToRepository()
    {
        var userId = Guid.NewGuid();
        var notificationId = Guid.NewGuid();
        _repository
            .Setup(repo => repo.MarkAsReadAsync(userId, notificationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Unit, NotificationError>.Success(Unit.Value));

        using var service = CreateService();

        var result = await service.MarkAsReadAsync(userId, notificationId);

        result.IsSuccess.Should().BeTrue();
        _repository.Verify(repo => repo.MarkAsReadAsync(userId, notificationId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DismissAsync_ShouldForwardUserIdToRepository()
    {
        var userId = Guid.NewGuid();
        var notificationId = Guid.NewGuid();
        _repository
            .Setup(repo => repo.DismissAsync(userId, notificationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Unit, NotificationError>.Success(Unit.Value));

        using var service = CreateService();

        var result = await service.DismissAsync(userId, notificationId);

        result.IsSuccess.Should().BeTrue();
        _repository.Verify(repo => repo.DismissAsync(userId, notificationId, It.IsAny<CancellationToken>()), Times.Once);
    }

    private NotificationCenterService CreateService()
    {
        return new NotificationCenterService(_repository.Object, NullLogger<NotificationCenterService>.Instance);
    }
}
