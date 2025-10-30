# Blazor Component Testing Guide

## Overview

This document provides guidance for writing and maintaining component tests for the DropBear.Codex.Blazor library using **bUnit** (Blazor Unit Testing framework).

## Testing Framework

- **bUnit** v1.34.41 - Blazor component testing framework
- **xUnit** v2.9.2 - Test runner
- **FluentAssertions** v8.8.0 - Fluent assertion library
- **Moq** v4.20.72 - Mocking framework

## Project Structure

```
DropBear.Codex.Blazor.Tests/
├── Components/              # Component tests
│   ├── ThemeToggleTests.cs
│   └── DropBearButtonTests.cs
├── TestHelpers/            # Test infrastructure
│   └── ComponentTestBase.cs
├── Builders/               # Builder pattern tests
├── Models/                 # Model tests
└── TESTING_GUIDE.md       # This document
```

## Getting Started

### Running Tests

```bash
# Run all tests
dotnet test

# Run tests with detailed output
dotnet test --verbosity detailed

# Run specific test class
dotnet test --filter "FullyQualifiedName~ThemeToggleTests"

# Run tests in watch mode
dotnet watch test
```

### Writing Your First Test

1. Create a test class inheriting from `ComponentTestBase`
2. Use bUnit's `RenderComponent<T>()` to render components
3. Use FluentAssertions for readable assertions

Example:

```csharp
public sealed class MyComponentTests : ComponentTestBase
{
    [Fact]
    public void MyComponent_Should_Render_WithDefaultProps()
    {
        // Arrange & Act
        var cut = RenderComponent<MyComponent>();

        // Assert
        cut.Should().NotBeNull();
        var element = cut.Find(".my-component");
        element.Should().NotBeNull();
    }
}
```

## Test Infrastructure

### ComponentTestBase

Base class providing common test infrastructure:

```csharp
public abstract class ComponentTestBase : TestContext, IDisposable
{
    protected Mock<ILogger> MockLogger { get; }

    // Automatically sets up:
    // - Mock logger
    // - JSInterop in loose mode
    // - Service collection
}
```

### Helper Methods

#### AddMockThemeService

Adds a fully configured mock theme service:

```csharp
[Fact]
public void Test_WithThemeService()
{
    // Arrange
    var mockThemeService = AddMockThemeService(Theme.Dark);

    // Act
    var cut = RenderComponent<ThemeToggle>();

    // Assert
    mockThemeService.Verify(x => x.InitializeAsync(It.IsAny<CancellationToken>()), Times.Once);
}
```

#### CreateTestLogger

Creates a silent logger for testing:

```csharp
var logger = CreateTestLogger();
Services.AddSingleton(logger);
```

## Testing Patterns

### 1. Basic Rendering Tests

Test that components render correctly with default props:

```csharp
[Fact]
public void Component_Should_Render_WithDefaultClasses()
{
    // Act
    var cut = RenderComponent<MyComponent>();

    // Assert
    var element = cut.Find(".my-component");
    element.ClassList.Should().Contain("my-component");
}
```

### 2. Parameter Testing

Test components with different parameter values:

```csharp
[Theory]
[InlineData("primary", "btn-primary")]
[InlineData("secondary", "btn-secondary")]
public void Button_Should_Apply_ColorClass(string color, string expectedClass)
{
    // Act
    var cut = RenderComponent<MyButton>(parameters => parameters
        .Add(p => p.Color, color));

    // Assert
    var button = cut.Find("button");
    button.ClassList.Should().Contain(expectedClass);
}
```

### 3. Event Handling Tests

Test that events are properly handled:

```csharp
[Fact]
public async Task Button_Should_InvokeOnClick_WhenClicked()
{
    // Arrange
    var clicked = false;
    var cut = RenderComponent<MyButton>(parameters => parameters
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
```

### 4. Accessibility Testing

Test ARIA attributes and accessibility features:

```csharp
[Fact]
public void Component_Should_HaveAccessibleAttributes()
{
    // Act
    var cut = RenderComponent<MyComponent>();

    // Assert
    var element = cut.Find(".my-component");
    element.HasAttribute("aria-label").Should().BeTrue();
    element.HasAttribute("role").Should().BeTrue();
    element.GetAttribute("role").Should().Be("button");
}
```

### 5. State Management Tests

Test component state changes:

```csharp
[Fact]
public void Component_Should_UpdateState_WhenPropertyChanges()
{
    // Arrange
    var cut = RenderComponent<MyComponent>(parameters => parameters
        .Add(p => p.IsActive, false));

    // Act
    cut.SetParametersAndRender(parameters => parameters
        .Add(p => p.IsActive, true));

    // Assert
    var element = cut.Find(".my-component");
    element.ClassList.Should().Contain("active");
}
```

### 6. Async Operation Tests

Test components with async operations:

```csharp
[Fact]
public async Task Component_Should_ShowLoading_DuringAsyncOperation()
{
    // Arrange
    var cut = RenderComponent<MyComponent>();

    // Act
    cut.Instance.LoadDataAsync();

    // Wait for loading state
    cut.WaitForState(() =>
    {
        var loader = cut.FindAll(".loader");
        return loader.Count > 0;
    }, TimeSpan.FromSeconds(2));

    // Assert
    var loader = cut.Find(".loader");
    loader.Should().NotBeNull();
}
```

### 7. Mocking Services

Test components that depend on services:

```csharp
[Fact]
public void Component_Should_UseService_WhenRendered()
{
    // Arrange
    var mockService = new Mock<IMyService>();
    mockService.Setup(x => x.GetData())
        .Returns(Task.FromResult("Test Data"));

    Services.AddSingleton(mockService.Object);

    // Act
    var cut = RenderComponent<MyComponent>();

    // Assert
    mockService.Verify(x => x.GetData(), Times.Once);
}
```

### 8. ChildContent Tests

Test components that accept child content:

```csharp
[Fact]
public void Component_Should_Render_WithChildContent()
{
    // Arrange
    const string childText = "Child Content";

    // Act
    var cut = RenderComponent<MyComponent>(parameters => parameters
        .AddChildContent(childText));

    // Assert
    cut.Markup.Should().Contain(childText);
}
```

### 9. Disposal Tests

Test proper resource cleanup:

```csharp
[Fact]
public void Component_Should_Dispose_WithoutErrors()
{
    // Arrange
    var cut = RenderComponent<MyComponent>();

    // Act & Assert
    var exception = Record.Exception(() => cut.Dispose());
    exception.Should().BeNull();
}
```

### 10. JSInterop Tests

Test JavaScript interop:

```csharp
[Fact]
public async Task Component_Should_CallJavaScript_WhenInitialized()
{
    // Arrange
    var jsInterop = JSInterop.SetupVoid("myFunction");

    // Act
    var cut = RenderComponent<MyComponent>();

    // Assert
    var invocations = JSInterop.Invocations["myFunction"];
    invocations.Should().HaveCount(1);
}
```

## Best Practices

### DO

✅ **Use descriptive test names** following the pattern: `Component_Should_Behavior_WhenCondition`

```csharp
[Fact]
public void Button_Should_BeDisabled_WhenIsLoadingIsTrue() { }
```

✅ **Use FluentAssertions** for readable assertions:

```csharp
result.Should().BeTrue();
collection.Should().HaveCount(5);
element.ClassList.Should().Contain("active");
```

✅ **Test one behavior per test**:

```csharp
// Good
[Fact]
public void Button_Should_BeDisabled_WhenIsDisabledIsTrue() { }

[Fact]
public void Button_Should_HaveDisabledClass_WhenIsDisabledIsTrue() { }

// Bad
[Fact]
public void Button_Should_HandleDisabledState()
{
    // Testing multiple behaviors in one test
}
```

✅ **Use Theory for parameterized tests**:

```csharp
[Theory]
[InlineData(ButtonColor.Primary, "btn-primary")]
[InlineData(ButtonColor.Secondary, "btn-secondary")]
public void Button_Should_Apply_ColorClass(ButtonColor color, string expectedClass) { }
```

✅ **Clean up resources**:

```csharp
public sealed class MyTests : ComponentTestBase
{
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Clean up test resources
        }
        base.Dispose(disposing);
    }
}
```

### DON'T

❌ **Don't test framework behavior**:

```csharp
// Bad - testing Blazor's parameter binding
[Fact]
public void Component_Should_ReceiveParameters() { }
```

❌ **Don't use Thread.Sleep** for async tests:

```csharp
// Bad
await button.ClickAsync();
Thread.Sleep(1000); // Never do this

// Good
await button.ClickAsync();
await Task.Delay(100); // Or use WaitForState
```

❌ **Don't test implementation details**:

```csharp
// Bad - testing private method names
[Fact]
public void Component_Should_Have_PrivateMethod() { }

// Good - testing public behavior
[Fact]
public void Component_Should_UpdateDisplay_WhenDataChanges() { }
```

❌ **Don't create brittle tests**:

```csharp
// Bad - too specific
cut.Markup.Should().Be("<div class=\"exact-html\">...</div>");

// Good - test behavior
cut.Find(".my-component").Should().NotBeNull();
```

## Common Patterns

### Testing with Parameters

```csharp
var cut = RenderComponent<MyComponent>(parameters => parameters
    .Add(p => p.Title, "Test Title")
    .Add(p => p.IsEnabled, true)
    .Add(p => p.OnClick, () => Task.CompletedTask)
    .AddChildContent("<span>Child</span>"));
```

### Testing with Unmatched Attributes

```csharp
var cut = RenderComponent<MyComponent>(parameters => parameters
    .AddUnmatched("class", "custom-class")
    .AddUnmatched("data-testid", "my-component"));
```

### Waiting for State Changes

```csharp
// Wait for element to appear
cut.WaitForState(() =>
{
    var elements = cut.FindAll(".my-element");
    return elements.Count > 0;
}, TimeSpan.FromSeconds(5));

// Wait for element to disappear
cut.WaitForState(() =>
{
    var elements = cut.FindAll(".loading");
    return elements.Count == 0;
}, TimeSpan.FromSeconds(5));
```

### Finding Elements

```csharp
// Find single element (throws if not found)
var button = cut.Find("button");
var specific = cut.Find("#my-id");
var byClass = cut.Find(".my-class");

// Find multiple elements
var buttons = cut.FindAll("button");
var items = cut.FindAll(".list-item");

// Check if element exists
var exists = cut.FindAll(".optional-element").Count > 0;
```

## Troubleshooting

### Issue: "Element not found"

**Problem**: Test fails because element hasn't rendered yet.

**Solution**: Use `WaitForState` or `WaitForAssertion`:

```csharp
cut.WaitForState(() => cut.FindAll(".my-element").Count > 0);
```

### Issue: "JSInterop not set up"

**Problem**: Component calls JavaScript but JSInterop isn't configured.

**Solution**: Set up JSInterop in arrange phase:

```csharp
JSInterop.Mode = JSRuntimeMode.Loose; // Already done in ComponentTestBase
JSInterop.SetupVoid("myFunction");
```

### Issue: "Service not found"

**Problem**: Component requires a service that isn't registered.

**Solution**: Register service in test:

```csharp
var mockService = new Mock<IMyService>();
Services.AddSingleton(mockService.Object);
```

### Issue: "Parameter validation failed"

**Problem**: Component requires certain parameters.

**Solution**: Provide all required parameters:

```csharp
var cut = RenderComponent<MyComponent>(parameters => parameters
    .Add(p => p.RequiredProp, "value"));
```

## CI/CD Integration

### GitHub Actions Example

```yaml
name: Run Tests

on: [push, pull_request]

jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '9.0.x'
      - name: Restore dependencies
        run: dotnet restore
      - name: Build
        run: dotnet build --no-restore
      - name: Test
        run: dotnet test --no-build --verbosity normal --logger "trx;LogFileName=test-results.trx"
      - name: Publish Test Results
        uses: EnricoMi/publish-unit-test-result-action@v2
        if: always()
        with:
          files: '**/test-results.trx'
```

## Code Coverage

### Generating Coverage Reports

```bash
# Install coverlet
dotnet tool install --global coverlet.console

# Run tests with coverage
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover

# Generate HTML report (requires reportgenerator)
dotnet tool install --global dotnet-reportgenerator-globaltool
reportgenerator -reports:coverage.opencover.xml -targetdir:coverage-report
```

### Coverage Goals

- **Minimum**: 70% code coverage
- **Target**: 80% code coverage
- **Critical components**: 90%+ code coverage

## Test Organization

### File Naming

- Test files should end with `Tests.cs`
- Match the component name: `ThemeToggle.razor` → `ThemeToggleTests.cs`

### Test Naming

Follow the pattern:
```
ComponentName_Should_Behavior_WhenCondition
```

Examples:
- `Button_Should_BeDisabled_WhenIsLoadingIsTrue`
- `Modal_Should_Close_WhenEscapeKeyPressed`
- `DataGrid_Should_DisplayRows_WhenDataIsProvided`

### Test Categories

Use traits to organize tests:

```csharp
[Trait("Category", "Unit")]
[Trait("Component", "ThemeToggle")]
public class ThemeToggleTests : ComponentTestBase { }
```

Run specific categories:
```bash
dotnet test --filter "Category=Unit"
```

## Resources

### Documentation

- [bUnit Documentation](https://bunit.dev)
- [xUnit Documentation](https://xunit.net)
- [FluentAssertions Documentation](https://fluentassertions.com)
- [Moq Documentation](https://github.com/moq/moq4)

### Examples

- [bUnit Samples](https://github.com/bUnit-dev/bUnit/tree/main/samples)
- [Blazor Testing Best Practices](https://bunit.dev/docs/getting-started/writing-tests.html)

### Community

- [bUnit GitHub](https://github.com/bUnit-dev/bUnit)
- [bUnit Discord](https://discord.gg/XJJgTSt)

---

## Test Statistics

**Current Status**:
- Total Tests: 81
- Passed: 81
- Failed: 0
- Success Rate: 100% ✅

**Coverage by Component**:
- ThemeToggle: 12/12 tests passing (100%) ✅
- DropBearButton: 35/35 tests passing (100%) ✅
- Models: 17/17 tests passing (100%) ✅
- Builders: 17/17 tests passing (100%) ✅

**Next Steps**:
1. Add tests for Modal components
2. Add tests for DataGrid component
3. Add tests for SelectionPanel component
4. Add tests for FileUploader component
5. Increase code coverage to 80%+

---

**Version**: 1.0.0
**Last Updated**: 2025-10-30
**Maintainer**: DropBear.Codex Team
