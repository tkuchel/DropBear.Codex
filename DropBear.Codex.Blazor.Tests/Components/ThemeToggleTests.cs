#region

using Bunit;
using DropBear.Codex.Blazor.Components.Buttons;
using DropBear.Codex.Blazor.Models;
using DropBear.Codex.Blazor.Tests.TestHelpers;
using FluentAssertions;
using Moq;
using Xunit;

#endregion

namespace DropBear.Codex.Blazor.Tests.Components;

/// <summary>
///     Tests for the ThemeToggle component.
/// </summary>
public sealed class ThemeToggleTests : ComponentTestBase
{
    [Fact]
    public void ThemeToggle_Should_Render_WithDefaultClasses()
    {
        // Arrange
        AddMockThemeService(Theme.Light);

        // Act
        var cut = RenderComponent<ThemeToggle>();

        // Assert
        var button = cut.Find("button");
        button.Should().NotBeNull();
        button.ClassList.Should().Contain("theme-toggle");
    }

    [Fact]
    public void ThemeToggle_Should_Apply_CustomCssClass()
    {
        // Arrange
        AddMockThemeService(Theme.Light);
        const string customClass = "my-custom-class";

        // Act
        var cut = RenderComponent<ThemeToggle>(parameters => parameters
            .Add(p => p.CssClass, customClass));

        // Assert
        var button = cut.Find("button");
        button.ClassList.Should().Contain(customClass);
    }

    [Fact]
    public void ThemeToggle_Should_ShowLightIcon_WhenThemeIsLight()
    {
        // Arrange
        AddMockThemeService(Theme.Light);

        // Act
        var cut = RenderComponent<ThemeToggle>();

        // Assert
        var darkIcon = cut.Find(".theme-toggle-dark-icon");
        darkIcon.Should().NotBeNull();
    }

    [Fact]
    public void ThemeToggle_Should_ShowDarkIcon_WhenThemeIsDark()
    {
        // Arrange
        AddMockThemeService(Theme.Dark);

        // Act
        var cut = RenderComponent<ThemeToggle>();

        // Assert - Need to wait for initialization
        cut.WaitForState(() =>
        {
            var lightIcon = cut.FindAll(".theme-toggle-light-icon");
            return lightIcon.Count > 0;
        }, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void ThemeToggle_Should_HaveAccessibleAttributes()
    {
        // Arrange
        AddMockThemeService(Theme.Light);

        // Act
        var cut = RenderComponent<ThemeToggle>();

        // Assert
        var button = cut.Find("button");
        button.HasAttribute("aria-label").Should().BeTrue();
        button.HasAttribute("title").Should().BeTrue();
        button.GetAttribute("type").Should().Be("button");
    }

    [Fact]
    public async Task ThemeToggle_Should_CallToggleThemeAsync_WhenClicked()
    {
        // Arrange
        var mockThemeService = AddMockThemeService(Theme.Light);
        var cut = RenderComponent<ThemeToggle>();

        // Act
        var button = cut.Find("button");
        await button.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        // Assert
        mockThemeService.Verify(x => x.ToggleThemeAsync(
            It.IsAny<bool>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ThemeToggle_Should_InvokeOnThemeChanged_WhenThemeChanges()
    {
        // Arrange
        var mockThemeService = AddMockThemeService(Theme.Light);
        var themeChanged = false;
        var newTheme = Theme.Light;

        var cut = RenderComponent<ThemeToggle>(parameters => parameters
            .Add(p => p.OnThemeChanged, (Theme theme) =>
            {
                themeChanged = true;
                newTheme = theme;
            }));

        // Act
        var button = cut.Find("button");
        await button.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        // Wait for async operations
        await Task.Delay(100);

        // Assert
        themeChanged.Should().BeTrue();
        newTheme.Should().Be(Theme.Dark); // Should toggle from Light to Dark
    }

    [Fact]
    public void ThemeToggle_Should_BeDisabled_WhenLoading()
    {
        // Arrange
        var mockThemeService = AddMockThemeService(Theme.Light);

        // Make initialization slow to keep loading state
        mockThemeService.Setup(x => x.InitializeAsync(It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                await Task.Delay(5000);
                return DropBear.Codex.Core.Results.Base.Result<ThemeInfo, Errors.JsInteropError>.Success(
                    new ThemeInfo
                    {
                        Current = "light",
                        Effective = "light",
                        UserPreference = "auto",
                        SystemTheme = "light",
                        IsAuto = false,
                        IsDark = false,
                        IsLight = true
                    });
            });

        // Act
        var cut = RenderComponent<ThemeToggle>();

        // Assert
        var button = cut.Find("button");
        button.HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public void ThemeToggle_Should_RespectAnimatedParameter()
    {
        // Arrange
        var mockThemeService = AddMockThemeService(Theme.Light);

        // Act
        var cut = RenderComponent<ThemeToggle>(parameters => parameters
            .Add(p => p.Animated, false));

        // Assert
        cut.Instance.Animated.Should().BeFalse();
    }

    [Fact]
    public async Task ThemeToggle_Should_UpdateAriaLabel_WhenThemeChanges()
    {
        // Arrange
        AddMockThemeService(Theme.Light);
        var cut = RenderComponent<ThemeToggle>();

        // Initial state
        var button = cut.Find("button");
        var initialLabel = button.GetAttribute("aria-label");

        // Act
        await button.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        // Wait for state change
        await Task.Delay(100);
        cut.Render();

        // Assert
        var updatedButton = cut.Find("button");
        var updatedLabel = updatedButton.GetAttribute("aria-label");
        updatedLabel.Should().NotBe(initialLabel);
    }

    [Fact]
    public async Task ThemeToggle_Should_HandleFailedThemeChange_Gracefully()
    {
        // Arrange
        var mockThemeService = AddMockThemeService(Theme.Light);
        mockThemeService.Setup(x => x.ToggleThemeAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DropBear.Codex.Core.Results.Base.Result<Theme, Errors.JsInteropError>.Failure(
                Errors.JsInteropError.InvocationFailed("toggleTheme", "Test error")));

        var cut = RenderComponent<ThemeToggle>();

        // Act
        var button = cut.Find("button");
        var exception = await Record.ExceptionAsync(async () =>
            await button.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs()));

        // Assert
        exception.Should().BeNull(); // Should not throw
    }

    [Fact]
    public void ThemeToggle_Should_DisposeCorrectly()
    {
        // Arrange
        var mockThemeService = AddMockThemeService(Theme.Light);
        var cut = RenderComponent<ThemeToggle>();

        // Act
        var exception = Record.Exception(() => cut.Dispose());

        // Assert
        exception.Should().BeNull();
    }
}
