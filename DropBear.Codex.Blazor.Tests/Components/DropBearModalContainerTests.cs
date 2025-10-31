#region

using Bunit;
using DropBear.Codex.Blazor.Components.Containers;
using DropBear.Codex.Blazor.Interfaces;
using DropBear.Codex.Blazor.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

#endregion

namespace DropBear.Codex.Blazor.Tests.Components;

/// <summary>
///     Tests for the DropBearModalContainer component.
/// </summary>
public sealed class DropBearModalContainerTests : ComponentTestBase
{
    private Mock<IModalService> _mockModalService = null!;

    public DropBearModalContainerTests()
    {
        SetupModalService();
    }

    private void SetupModalService(bool isVisible = false)
    {
        _mockModalService = new Mock<IModalService>();
        _mockModalService.Setup(x => x.IsModalVisible).Returns(isVisible);
        _mockModalService.Setup(x => x.CurrentComponent).Returns((Type?)null);
        _mockModalService.Setup(x => x.CurrentParameters).Returns((IDictionary<string, object>?)null);
        Services.AddSingleton(_mockModalService.Object);
    }

    [Fact]
    public void ModalContainer_Should_NotRender_WhenModalIsNotVisible()
    {
        // Arrange & Act
        var cut = RenderComponent<DropBearModalContainer>();

        // Assert
        var modals = cut.FindAll(".modal-overlay");
        modals.Should().BeEmpty();
    }

    [Fact]
    public void ModalContainer_Should_Render_WhenModalIsVisible()
    {
        // Arrange
        SetupModalService(isVisible: true);
        _mockModalService.Setup(x => x.CurrentComponent).Returns(typeof(TestComponent));

        // Act
        var cut = RenderComponent<DropBearModalContainer>();

        // Assert
        var modal = cut.Find(".modal-overlay");
        modal.Should().NotBeNull();
    }

    [Fact]
    public void ModalContainer_Should_HaveCorrectAriaAttributes()
    {
        // Arrange
        SetupModalService(isVisible: true);
        _mockModalService.Setup(x => x.CurrentComponent).Returns(typeof(TestComponent));

        // Act
        var cut = RenderComponent<DropBearModalContainer>();

        // Assert
        var overlay = cut.Find(".modal-overlay");
        overlay.GetAttribute("role").Should().Be("dialog");
        overlay.GetAttribute("aria-modal").Should().Be("true");
        overlay.HasAttribute("aria-labelledby").Should().BeTrue();
    }

    [Fact]
    public void ModalContainer_Should_ApplyEnterTransitionClass()
    {
        // Arrange
        SetupModalService(isVisible: true);
        _mockModalService.Setup(x => x.CurrentComponent).Returns(typeof(TestComponent));

        // Act
        var cut = RenderComponent<DropBearModalContainer>();

        // Assert
        var content = cut.Find(".modal-content");
        content.ClassList.Should().Contain("enter");
    }

    [Fact]
    public void ModalContainer_Should_RenderDynamicComponent_WhenComponentTypeProvided()
    {
        // Arrange
        SetupModalService(isVisible: true);
        _mockModalService.Setup(x => x.CurrentComponent).Returns(typeof(TestComponent));

        // Act
        var cut = RenderComponent<DropBearModalContainer>();

        // Assert
        var dynamicComponent = cut.FindComponent<DynamicComponent>();
        dynamicComponent.Should().NotBeNull();
    }

    [Fact]
    public async Task ModalContainer_Should_CallCloseModal_WhenOverlayClicked()
    {
        // Arrange
        SetupModalService(isVisible: true);
        _mockModalService.Setup(x => x.CurrentComponent).Returns(typeof(TestComponent));
        _mockModalService.Setup(x => x.ClearAll()).Verifiable();

        var cut = RenderComponent<DropBearModalContainer>();

        // Act
        var overlay = cut.Find(".modal-overlay");
        await overlay.ClickAsync(new MouseEventArgs());

        // Assert
        _mockModalService.Verify(x => x.ClearAll(), Times.Once);
    }

    [Fact]
    public async Task ModalContainer_Should_NotCloseModal_WhenContentClicked()
    {
        // Arrange
        SetupModalService(isVisible: true);
        _mockModalService.Setup(x => x.CurrentComponent).Returns(typeof(TestComponent));
        _mockModalService.Setup(x => x.ClearAll()).Verifiable();

        var cut = RenderComponent<DropBearModalContainer>();

        // Act
        var content = cut.Find(".modal-content");
        await content.ClickAsync(new MouseEventArgs());

        // Assert - Should not be called because stopPropagation is set
        _mockModalService.Verify(x => x.ClearAll(), Times.Never);
    }

    [Fact]
    public async Task ModalContainer_Should_CloseModal_OnEscapeKey()
    {
        // Arrange
        SetupModalService(isVisible: true);
        _mockModalService.Setup(x => x.CurrentComponent).Returns(typeof(TestComponent));
        _mockModalService.Setup(x => x.ClearAll()).Verifiable();

        var cut = RenderComponent<DropBearModalContainer>();

        // Act
        var overlay = cut.Find(".modal-overlay");
        await overlay.KeyDownAsync(new KeyboardEventArgs { Key = "Escape" });

        // Assert
        _mockModalService.Verify(x => x.ClearAll(), Times.Once);
    }

    [Fact]
    public async Task ModalContainer_Should_NotCloseModal_OnNonEscapeKey()
    {
        // Arrange
        SetupModalService(isVisible: true);
        _mockModalService.Setup(x => x.CurrentComponent).Returns(typeof(TestComponent));
        _mockModalService.Setup(x => x.ClearAll()).Verifiable();

        var cut = RenderComponent<DropBearModalContainer>();

        // Act
        var overlay = cut.Find(".modal-overlay");
        await overlay.KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });

        // Assert
        _mockModalService.Verify(x => x.ClearAll(), Times.Never);
    }

    [Fact]
    public void ModalContainer_Should_ApplyCustomWidth_WhenProvided()
    {
        // Arrange
        var customWidth = "800px";
        SetupModalService(isVisible: true);
        _mockModalService.Setup(x => x.CurrentComponent).Returns(typeof(TestComponent));
        _mockModalService.Setup(x => x.CurrentParameters).Returns(new Dictionary<string, object>
        {
            { "CustomWidth", customWidth }
        });

        // Act
        var cut = RenderComponent<DropBearModalContainer>();

        // Assert
        var content = cut.Find(".modal-content");
        var style = content.GetAttribute("style");
        style.Should().Contain(customWidth);
    }

    [Fact]
    public void ModalContainer_Should_ApplyCustomHeight_WhenProvided()
    {
        // Arrange
        var customHeight = "600px";
        SetupModalService(isVisible: true);
        _mockModalService.Setup(x => x.CurrentComponent).Returns(typeof(TestComponent));
        _mockModalService.Setup(x => x.CurrentParameters).Returns(new Dictionary<string, object>
        {
            { "CustomHeight", customHeight }
        });

        // Act
        var cut = RenderComponent<DropBearModalContainer>();

        // Assert
        var content = cut.Find(".modal-content");
        var style = content.GetAttribute("style");
        style.Should().Contain(customHeight);
    }

    [Fact]
    public void ModalContainer_Should_FilterOutContainerParameters()
    {
        // Arrange
        SetupModalService(isVisible: true);
        _mockModalService.Setup(x => x.CurrentComponent).Returns(typeof(TestComponent));
        _mockModalService.Setup(x => x.CurrentParameters).Returns(new Dictionary<string, object>
        {
            { "CustomWidth", "800px" },
            { "CustomHeight", "600px" },
            { "TransitionDuration", "0.5s" }
        });

        // Act
        var cut = RenderComponent<DropBearModalContainer>();

        // Assert - Component should render without error and filter container parameters
        var dynamicComponent = cut.FindComponent<DynamicComponent>();
        dynamicComponent.Should().NotBeNull();
    }

    [Fact]
    public void ModalContainer_Should_DisposeCorrectly()
    {
        // Arrange
        SetupModalService(isVisible: true);
        _mockModalService.Setup(x => x.CurrentComponent).Returns(typeof(TestComponent));
        var cut = RenderComponent<DropBearModalContainer>();

        // Act
        var exception = Record.Exception(() => cut.Dispose());

        // Assert
        exception.Should().BeNull();
    }

    [Fact]
    public void ModalContainer_Should_SubscribeToModalServiceEvents()
    {
        // Arrange & Act
        var cut = RenderComponent<DropBearModalContainer>();

        // Assert - Component should subscribe to OnChange event
        _mockModalService.VerifyAdd(x => x.OnChange += It.IsAny<Action>(), Times.Once);
    }

    [Fact]
    public void ModalContainer_Should_RenderCorrectly()
    {
        // Arrange & Act
        var cut = RenderComponent<DropBearModalContainer>();

        // Assert - Component should render without errors
        cut.Instance.Should().NotBeNull();
    }

    /// <summary>
    ///     Simple test component for modal testing.
    /// </summary>
    private class TestComponent : ComponentBase
    {
        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
        {
            builder.AddContent(0, "Test Modal Content");
        }
    }
}
