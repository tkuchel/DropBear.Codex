#region

using Bunit;
using DropBear.Codex.Blazor.Interfaces;
using DropBear.Codex.Blazor.Models;
using DropBear.Codex.Core.Results.Base;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Serilog;
using Serilog.Core;
using Xunit;

#endregion

namespace DropBear.Codex.Blazor.Tests.TestHelpers;

/// <summary>
///     Base class for Blazor component tests using bUnit.
///     Provides common test infrastructure, service mocking, and cleanup.
/// </summary>
public abstract class ComponentTestBase : TestContext, IDisposable
{
    /// <summary>
    ///     Gets the mock logger for testing.
    /// </summary>
    protected Mock<ILogger> MockLogger { get; }

    /// <summary>
    ///     Initializes a new instance of the <see cref="ComponentTestBase"/> class.
    /// </summary>
    protected ComponentTestBase()
    {
        // Setup mock logger
        MockLogger = new Mock<ILogger>();
        Services.AddSingleton(MockLogger.Object);

        // Setup JSInterop for testing
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    /// <summary>
    ///     Adds a mock theme service to the service collection.
    /// </summary>
    /// <param name="initialTheme">The initial theme to return.</param>
    /// <returns>The mock theme service.</returns>
    protected Mock<IThemeService> AddMockThemeService(Theme initialTheme = Theme.Light)
    {
        var mockThemeService = new Mock<IThemeService>();

        // Setup default behaviors
        mockThemeService.Setup(x => x.IsInitialized).Returns(true);

        mockThemeService.Setup(x => x.InitializeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ThemeInfo, Errors.JsInteropError>.Success(new ThemeInfo
            {
                Current = initialTheme.ToString().ToLowerInvariant(),
                Effective = initialTheme.ToString().ToLowerInvariant(),
                UserPreference = "auto",
                SystemTheme = "light",
                IsAuto = false,
                IsDark = initialTheme == Theme.Dark,
                IsLight = initialTheme == Theme.Light
            }));

        mockThemeService.Setup(x => x.GetThemeInfoAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ThemeInfo, Errors.JsInteropError>.Success(new ThemeInfo
            {
                Current = initialTheme.ToString().ToLowerInvariant(),
                Effective = initialTheme.ToString().ToLowerInvariant(),
                UserPreference = "auto",
                SystemTheme = "light",
                IsAuto = false,
                IsDark = initialTheme == Theme.Dark,
                IsLight = initialTheme == Theme.Light
            }));

        mockThemeService.Setup(x => x.SetThemeAsync(It.IsAny<Theme>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<bool, Errors.JsInteropError>.Success(true));

        mockThemeService.Setup(x => x.ToggleThemeAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((bool animated, CancellationToken ct) =>
            {
                var newTheme = initialTheme == Theme.Light ? Theme.Dark : Theme.Light;
                return Result<Theme, Errors.JsInteropError>.Success(newTheme);
            });

        Services.AddSingleton(mockThemeService.Object);
        return mockThemeService;
    }

    /// <summary>
    ///     Creates a test logger that doesn't write to any output.
    /// </summary>
    /// <returns>A silent logger for testing.</returns>
    protected static ILogger CreateTestLogger()
    {
        return new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .CreateLogger();
    }

    /// <summary>
    ///     Disposes test resources.
    /// </summary>
    public new void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Disposes test resources.
    /// </summary>
    /// <param name="disposing">Whether to dispose managed resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            base.Dispose();
        }
    }
}
