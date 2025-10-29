using DropBear.Codex.Blazor.Builders;
using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Models;
using FluentAssertions;

namespace DropBear.Codex.Blazor.Tests.Builders;

public sealed class SnackbarInstanceBuilderTests
{
    #region Basic Building Tests

    [Fact]
    public void Build_WithMessage_ShouldCreateSnackbar()
    {
        // Arrange
        var builder = new SnackbarInstanceBuilder();

        // Act
        var result = builder
            .WithMessage("Test message")
            .Build();

        // Assert
        result.Should().NotBeNull();
        result.Message.Should().Be("Test message");
        result.Id.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Build_WithoutMessage_ShouldThrow()
    {
        // Arrange
        var builder = new SnackbarInstanceBuilder();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    [Fact]
    public void WithId_ShouldSetCustomId()
    {
        // Arrange
        var customId = "custom-snackbar-id";

        // Act
        var result = new SnackbarInstanceBuilder()
            .WithId(customId)
            .WithMessage("Test")
            .Build();

        // Assert
        result.Id.Should().Be(customId);
    }

    [Fact]
    public void WithId_WithNull_ShouldThrow()
    {
        // Arrange
        var builder = new SnackbarInstanceBuilder();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => builder.WithId(null!));
    }

    #endregion

    #region Property Configuration Tests

    [Fact]
    public void WithTitle_ShouldSetTitle()
    {
        // Act
        var result = new SnackbarInstanceBuilder()
            .WithMessage("Message")
            .WithTitle("Test Title")
            .Build();

        // Assert
        result.Title.Should().Be("Test Title");
    }

    [Fact]
    public void WithType_ShouldSetType()
    {
        // Act
        var result = new SnackbarInstanceBuilder()
            .WithMessage("Message")
            .WithType(SnackbarType.Success)
            .Build();

        // Assert
        result.Type.Should().Be(SnackbarType.Success);
    }

    [Fact]
    public void WithDuration_ShouldSetDuration()
    {
        // Act
        var result = new SnackbarInstanceBuilder()
            .WithMessage("Message")
            .WithDuration(3000)
            .Build();

        // Assert
        result.Duration.Should().Be(3000);
    }

    [Fact]
    public void WithDuration_WithNegative_ShouldUseZero()
    {
        // Act
        var result = new SnackbarInstanceBuilder()
            .WithMessage("Message")
            .WithDuration(-1000)
            .Build();

        // Assert
        result.Duration.Should().Be(0);
    }

    [Fact]
    public void WithDelay_ShouldSetDelay()
    {
        // Act
        var result = new SnackbarInstanceBuilder()
            .WithMessage("Message")
            .WithDelay(1000)
            .Build();

        // Assert
        result.ShowDelay.Should().Be(1000);
    }

    [Fact]
    public void WithDelay_WithNegative_ShouldUseZero()
    {
        // Act
        var result = new SnackbarInstanceBuilder()
            .WithMessage("Message")
            .WithDelay(-500)
            .Build();

        // Assert
        result.ShowDelay.Should().Be(0);
    }

    [Fact]
    public void WithDelay_WithTooLarge_ShouldCapAt10000()
    {
        // Act
        var result = new SnackbarInstanceBuilder()
            .WithMessage("Message")
            .WithDelay(15000)
            .Build();

        // Assert
        result.ShowDelay.Should().Be(10000);
    }

    [Fact]
    public void RequireManualClose_ShouldSetFlag()
    {
        // Act
        var result = new SnackbarInstanceBuilder()
            .WithMessage("Message")
            .RequireManualClose()
            .Build();

        // Assert
        result.RequiresManualClose.Should().BeTrue();
    }

    [Fact]
    public void WithCssClass_ShouldSetCssClass()
    {
        // Act
        var result = new SnackbarInstanceBuilder()
            .WithMessage("Message")
            .WithCssClass("custom-class")
            .Build();

        // Assert
        result.CssClass.Should().Be("custom-class");
    }

    #endregion

    #region Action Tests

    [Fact]
    public void WithAction_WithSnackbarAction_ShouldAddAction()
    {
        // Arrange
        var action = new SnackbarAction
        {
            Label = "Click me",
            OnClick = () => Task.CompletedTask,
            IsPrimary = true
        };

        // Act
        var result = new SnackbarInstanceBuilder()
            .WithMessage("Message")
            .WithAction(action)
            .Build();

        // Assert
        result.Actions.Should().NotBeNull();
        result.Actions.Should().HaveCount(1);
        result.Actions![0].Label.Should().Be("Click me");
        result.Actions[0].IsPrimary.Should().BeTrue();
    }

    [Fact]
    public void WithAction_WithParameters_ShouldAddAction()
    {
        // Act
        var result = new SnackbarInstanceBuilder()
            .WithMessage("Message")
            .WithAction("Dismiss", () => Task.CompletedTask, isPrimary: true)
            .Build();

        // Assert
        result.Actions.Should().NotBeNull();
        result.Actions.Should().HaveCount(1);
        result.Actions![0].Label.Should().Be("Dismiss");
        result.Actions[0].IsPrimary.Should().BeTrue();
    }

    [Fact]
    public void WithAction_WithNull_ShouldThrow()
    {
        // Arrange
        var builder = new SnackbarInstanceBuilder();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => builder.WithAction((SnackbarAction)null!));
    }

    [Fact]
    public void WithActions_ShouldAddMultipleActions()
    {
        // Arrange
        var action1 = new SnackbarAction { Label = "Action 1", OnClick = () => Task.CompletedTask };
        var action2 = new SnackbarAction { Label = "Action 2", OnClick = () => Task.CompletedTask };

        // Act
        var result = new SnackbarInstanceBuilder()
            .WithMessage("Message")
            .WithActions(action1, action2)
            .Build();

        // Assert
        result.Actions.Should().HaveCount(2);
        result.Actions![0].Label.Should().Be("Action 1");
        result.Actions[1].Label.Should().Be("Action 2");
    }

    #endregion

    #region Metadata Tests

    [Fact]
    public void WithMetadata_ShouldAddMetadata()
    {
        // Act
        var result = new SnackbarInstanceBuilder()
            .WithMessage("Message")
            .WithMetadata("key1", "value1")
            .Build();

        // Assert
        result.Metadata.Should().NotBeNull();
        result.Metadata.Should().ContainKey("key1");
        result.Metadata!["key1"].Should().Be("value1");
    }

    [Fact]
    public void WithMetadata_WithNullKey_ShouldThrow()
    {
        // Arrange
        var builder = new SnackbarInstanceBuilder();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => builder.WithMetadata(null!, "value"));
    }

    [Fact]
    public void WithMetadata_WithNullValue_ShouldThrow()
    {
        // Arrange
        var builder = new SnackbarInstanceBuilder();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => builder.WithMetadata("key", null!));
    }

    [Fact]
    public void WithMetadata_WithDictionary_ShouldAddAllEntries()
    {
        // Arrange
        var metadata = new Dictionary<string, object>
        {
            ["key1"] = "value1",
            ["key2"] = 42
        };

        // Act
        var result = new SnackbarInstanceBuilder()
            .WithMessage("Message")
            .WithMetadata(metadata)
            .Build();

        // Assert
        result.Metadata.Should().HaveCount(2);
        result.Metadata!["key1"].Should().Be("value1");
        result.Metadata["key2"].Should().Be(42);
    }

    #endregion

    #region Fluent Interface Tests

    [Fact]
    public void FluentChaining_ShouldWorkCorrectly()
    {
        // Act
        var result = new SnackbarInstanceBuilder()
            .WithId("test-id")
            .WithTitle("Title")
            .WithMessage("Message")
            .WithType(SnackbarType.Warning)
            .WithDuration(2000)
            .WithDelay(500)
            .RequireManualClose()
            .WithCssClass("custom")
            .WithMetadata("test", "value")
            .Build();

        // Assert
        result.Id.Should().Be("test-id");
        result.Title.Should().Be("Title");
        result.Message.Should().Be("Message");
        result.Type.Should().Be(SnackbarType.Warning);
        result.Duration.Should().Be(2000);
        result.ShowDelay.Should().Be(500);
        result.RequiresManualClose.Should().BeTrue();
        result.CssClass.Should().Be("custom");
        result.Metadata.Should().ContainKey("test");
        result.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    #endregion
}
