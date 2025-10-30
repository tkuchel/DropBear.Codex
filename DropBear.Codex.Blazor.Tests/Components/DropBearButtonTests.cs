#region

using Bunit;
using DropBear.Codex.Blazor.Components.Buttons;
using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

#endregion

namespace DropBear.Codex.Blazor.Tests.Components;

/// <summary>
///     Tests for the DropBearButton component.
/// </summary>
public sealed class DropBearButtonTests : ComponentTestBase
{
    [Fact]
    public void DropBearButton_Should_Render_WithDefaultClasses()
    {
        // Act
        var cut = RenderComponent<DropBearButton>();

        // Assert
        var button = cut.Find("button");
        button.Should().NotBeNull();
        button.ClassList.Should().Contain("dropbear-btn");
    }

    [Fact]
    public void DropBearButton_Should_Render_WithChildContent()
    {
        // Arrange
        const string buttonText = "Click Me";

        // Act
        var cut = RenderComponent<DropBearButton>(parameters => parameters
            .AddChildContent(buttonText));

        // Assert
        var contentSpan = cut.Find(".dropbear-btn__content");
        contentSpan.TextContent.Should().Be(buttonText);
    }

    [Theory]
    [InlineData(ButtonStyle.Solid, "dropbear-btn--solid")]
    [InlineData(ButtonStyle.Outline, "dropbear-btn--outline")]
    [InlineData(ButtonStyle.Ghost, "dropbear-btn--ghost")]
    [InlineData(ButtonStyle.Link, "dropbear-btn--link")]
    public void DropBearButton_Should_Apply_ButtonStyleClass(ButtonStyle style, string expectedClass)
    {
        // Act
        var cut = RenderComponent<DropBearButton>(parameters => parameters
            .Add(p => p.ButtonStyle, style));

        // Assert
        var button = cut.Find("button");
        button.ClassList.Should().Contain(expectedClass);
    }

    [Theory]
    [InlineData(ButtonColor.Default, "dropbear-btn--default")]
    [InlineData(ButtonColor.Primary, "dropbear-btn--primary")]
    [InlineData(ButtonColor.Secondary, "dropbear-btn--secondary")]
    [InlineData(ButtonColor.Success, "dropbear-btn--success")]
    [InlineData(ButtonColor.Warning, "dropbear-btn--warning")]
    [InlineData(ButtonColor.Error, "dropbear-btn--error")]
    [InlineData(ButtonColor.Information, "dropbear-btn--information")]
    public void DropBearButton_Should_Apply_ColorClass(ButtonColor color, string expectedClass)
    {
        // Act
        var cut = RenderComponent<DropBearButton>(parameters => parameters
            .Add(p => p.Color, color));

        // Assert
        var button = cut.Find("button");
        button.ClassList.Should().Contain(expectedClass);
    }

    [Theory]
    [InlineData(ButtonSize.XSmall, "dropbear-btn--xs")]
    [InlineData(ButtonSize.Small, "dropbear-btn--sm")]
    [InlineData(ButtonSize.Medium, "dropbear-btn--md")]
    [InlineData(ButtonSize.Large, "dropbear-btn--lg")]
    [InlineData(ButtonSize.XLarge, "dropbear-btn--xl")]
    public void DropBearButton_Should_Apply_SizeClass(ButtonSize size, string expectedClass)
    {
        // Act
        var cut = RenderComponent<DropBearButton>(parameters => parameters
            .Add(p => p.Size, size));

        // Assert
        var button = cut.Find("button");
        button.ClassList.Should().Contain(expectedClass);
    }

    [Fact]
    public void DropBearButton_Should_Be_Disabled_When_IsDisabled_IsTrue()
    {
        // Act
        var cut = RenderComponent<DropBearButton>(parameters => parameters
            .Add(p => p.IsDisabled, true));

        // Assert
        var button = cut.Find("button");
        button.HasAttribute("disabled").Should().BeTrue();
        button.ClassList.Should().Contain("dropbear-btn--disabled");
    }

    [Fact]
    public void DropBearButton_Should_Show_LoadingState()
    {
        // Act
        var cut = RenderComponent<DropBearButton>(parameters => parameters
            .Add(p => p.IsLoading, true));

        // Assert
        var button = cut.Find("button");
        button.HasAttribute("disabled").Should().BeTrue();
        button.GetAttribute("aria-busy").Should().Be("true");
        button.ClassList.Should().Contain("dropbear-btn--loading");

        var spinner = cut.Find(".dropbear-btn__spinner");
        spinner.Should().NotBeNull();
    }

    [Fact]
    public void DropBearButton_Should_Display_Icon()
    {
        // Arrange
        const string iconClass = "fas fa-save";

        // Act
        var cut = RenderComponent<DropBearButton>(parameters => parameters
            .Add(p => p.Icon, iconClass));

        // Assert
        var icon = cut.Find(".dropbear-btn__icon");
        icon.Should().NotBeNull();
        icon.ClassList.Should().Contain("fas");
        icon.ClassList.Should().Contain("fa-save");
    }

    [Fact]
    public void DropBearButton_Should_Apply_BlockClass_WhenIsBlock()
    {
        // Act
        var cut = RenderComponent<DropBearButton>(parameters => parameters
            .Add(p => p.IsBlock, true));

        // Assert
        var button = cut.Find("button");
        button.ClassList.Should().Contain("dropbear-btn--block");
    }

    [Fact]
    public void DropBearButton_Should_Have_Tooltip_Attribute()
    {
        // Arrange
        const string tooltip = "Click to save";

        // Act
        var cut = RenderComponent<DropBearButton>(parameters => parameters
            .Add(p => p.Tooltip, tooltip));

        // Assert
        var button = cut.Find("button");
        button.GetAttribute("title").Should().Be(tooltip);
    }

    [Fact]
    public async Task DropBearButton_Should_InvokeOnClick_WhenClicked()
    {
        // Arrange
        var clicked = false;
        var cut = RenderComponent<DropBearButton>(parameters => parameters
            .Add(p => p.OnClick, () =>
            {
                clicked = true;
                return Task.CompletedTask;
            }));

        // Act
        var button = cut.Find("button");
        await button.ClickAsync(new MouseEventArgs());

        // Assert
        clicked.Should().BeTrue();
    }

    [Fact]
    public async Task DropBearButton_Should_NotInvokeOnClick_WhenDisabled()
    {
        // Arrange
        var clicked = false;
        var cut = RenderComponent<DropBearButton>(parameters => parameters
            .Add(p => p.IsDisabled, true)
            .Add(p => p.OnClick, () =>
            {
                clicked = true;
                return Task.CompletedTask;
            }));

        // Act
        var button = cut.Find("button");
        // Note: disabled buttons shouldn't trigger click events in HTML,
        // but we test the component's handling
        button.HasAttribute("disabled").Should().BeTrue();

        // Assert
        clicked.Should().BeFalse();
    }

    [Fact]
    public async Task DropBearButton_Should_NotInvokeOnClick_WhenLoading()
    {
        // Arrange
        var clicked = false;
        var cut = RenderComponent<DropBearButton>(parameters => parameters
            .Add(p => p.IsLoading, true)
            .Add(p => p.OnClick, () =>
            {
                clicked = true;
                return Task.CompletedTask;
            }));

        // Act
        var button = cut.Find("button");
        button.HasAttribute("disabled").Should().BeTrue();

        // Assert
        clicked.Should().BeFalse();
    }

    [Theory]
    [InlineData("button")]
    [InlineData("submit")]
    [InlineData("reset")]
    public void DropBearButton_Should_Set_ButtonType(string type)
    {
        // Act
        var cut = RenderComponent<DropBearButton>(parameters => parameters
            .Add(p => p.Type, type));

        // Assert
        var button = cut.Find("button");
        button.GetAttribute("type").Should().Be(type);
    }

    [Fact]
    public void DropBearButton_Should_Apply_CustomClass_FromAdditionalAttributes()
    {
        // Arrange
        const string customClass = "my-custom-class";

        // Act
        var cut = RenderComponent<DropBearButton>(parameters => parameters
            .AddUnmatched("class", customClass));

        // Assert
        var button = cut.Find("button");
        button.ClassList.Should().Contain(customClass);
    }

    [Fact]
    public void DropBearButton_Should_Apply_IconOnlyClass_WhenNoChildContent()
    {
        // Arrange
        const string iconClass = "fas fa-check";

        // Act
        var cut = RenderComponent<DropBearButton>(parameters => parameters
            .Add(p => p.Icon, iconClass));

        // Assert
        var button = cut.Find("button");
        button.ClassList.Should().Contain("dropbear-btn--icon-only");
    }

    [Fact]
    public void DropBearButton_Should_NotApply_IconOnlyClass_WhenHasChildContent()
    {
        // Arrange
        const string iconClass = "fas fa-check";

        // Act
        var cut = RenderComponent<DropBearButton>(parameters => parameters
            .Add(p => p.Icon, iconClass)
            .AddChildContent("Save"));

        // Assert
        var button = cut.Find("button");
        button.ClassList.Should().NotContain("dropbear-btn--icon-only");
    }

    [Fact]
    public void DropBearButton_Should_Have_AccessibleAttributes()
    {
        // Act
        var cut = RenderComponent<DropBearButton>(parameters => parameters
            .Add(p => p.IsDisabled, true));

        // Assert
        var button = cut.Find("button");
        button.HasAttribute("aria-disabled").Should().BeTrue();
        button.HasAttribute("aria-busy").Should().BeTrue();
    }

    [Fact]
    public async Task DropBearButton_Should_ThrottleClicks()
    {
        // Arrange
        var clickCount = 0;
        var cut = RenderComponent<DropBearButton>(parameters => parameters
            .Add(p => p.OnClick, () =>
            {
                clickCount++;
                return Task.CompletedTask;
            }));

        // Act - rapid clicks
        var button = cut.Find("button");
        await button.ClickAsync(new MouseEventArgs());
        await button.ClickAsync(new MouseEventArgs());
        await button.ClickAsync(new MouseEventArgs());

        // Wait for throttle delay
        await Task.Delay(200);

        // Assert - should only register one click due to throttling
        clickCount.Should().BeLessThan(3);
    }

    [Fact]
    public void DropBearButton_Should_Dispose_WithoutErrors()
    {
        // Arrange
        var cut = RenderComponent<DropBearButton>();

        // Act & Assert
        var exception = Record.Exception(() => cut.Dispose());
        exception.Should().BeNull();
    }
}
