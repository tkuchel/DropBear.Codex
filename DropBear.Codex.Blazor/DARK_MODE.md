# Dark Mode Implementation Guide

## Overview

The DropBear.Codex.Blazor library includes a comprehensive dark mode system with automatic system preference detection, smooth transitions, persistent storage, and component-level theme overrides.

## Features

- ✅ **System Preference Detection** - Automatically detects `prefers-color-scheme` from OS/browser
- ✅ **Manual Theme Control** - Light, Dark, or Auto modes
- ✅ **Smooth Transitions** - Animated theme changes with reduced motion support
- ✅ **Persistent Storage** - Theme preference saved to localStorage
- ✅ **Multi-Tab Sync** - Theme changes propagate across browser tabs
- ✅ **Component-Level Override** - Apply different themes to specific components
- ✅ **Accessibility** - Full support for reduced motion and high contrast modes
- ✅ **FOUC Prevention** - No flash of unstyled content on page load

---

## Quick Start

### 1. Register the ThemeService

In your `Program.cs` or `Startup.cs`:

```csharp
using DropBear.Codex.Blazor.Interfaces;
using DropBear.Codex.Blazor.Services;

// Register as singleton for application-wide theme management
builder.Services.AddSingleton<IThemeService, ThemeService>();
```

### 2. Include CSS Files

In your `_Host.cshtml`, `index.html`, or `App.razor`:

```html
<!-- Modern design system with theme variables -->
<link rel="stylesheet" href="styles/design-system-modern.css" />

<!-- Theme transitions and component-level overrides -->
<link rel="stylesheet" href="styles/theme-transitions.css" />
```

### 3. Add ThemeToggle Component

```razor
@using DropBear.Codex.Blazor.Components.Buttons

<ThemeToggle Animated="true" />
```

---

## Architecture

### Components

```
┌─────────────────────────────────────────────────┐
│  DropBearThemeManager.module.js                 │
│  - System preference detection                  │
│  - localStorage management                      │
│  - Theme application logic                      │
│  - Multi-tab synchronization                    │
└─────────────────────────────────────────────────┘
                    ↕ JS Interop
┌─────────────────────────────────────────────────┐
│  ThemeService.cs (IThemeService)                │
│  - Blazor C# wrapper                            │
│  - Result pattern error handling                │
│  - Async/await support                          │
│  - Event notifications                          │
└─────────────────────────────────────────────────┘
                    ↕ Used by
┌─────────────────────────────────────────────────┐
│  ThemeToggle.razor                              │
│  - Pre-built toggle button component           │
│  - Animated transitions                         │
│  - Accessible (aria-label, title)              │
└─────────────────────────────────────────────────┘
```

### CSS Architecture

```css
:root {
  /* Base color scales (50-900) */
  --color-gray-50: #f9fafb;
  --color-gray-900: #111827;

  /* Semantic tokens (light mode defaults) */
  --surface-primary: #ffffff;
  --text-primary: var(--color-gray-900);
}

/* Dark mode overrides */
[data-theme="dark"] {
  --surface-primary: var(--color-gray-900);
  --text-primary: var(--color-gray-50);
}

/* Component-level override */
.my-component[data-theme="dark"] {
  /* This component is dark even if app is light */
}
```

---

## Usage Examples

### Basic Theme Toggle

```razor
@page "/example"
@inject IThemeService ThemeService

<button @onclick="ToggleThemeAsync">Toggle Theme</button>

@code {
    private async Task ToggleThemeAsync()
    {
        var result = await ThemeService.ToggleThemeAsync(animated: true);
        if (result.IsSuccess)
        {
            Console.WriteLine($"Theme changed to: {result.Value}");
        }
    }
}
```

### Set Specific Theme

```csharp
// Set to light mode with animation
await ThemeService.SetThemeAsync(Theme.Light, animated: true);

// Set to dark mode instantly (no animation)
await ThemeService.SetThemeAsync(Theme.Dark, animated: false);

// Set to auto (follow system preference)
await ThemeService.SetThemeAsync(Theme.Auto, animated: true);
```

### Get Current Theme Information

```csharp
var themeInfoResult = await ThemeService.GetThemeInfoAsync();
if (themeInfoResult.IsSuccess)
{
    var info = themeInfoResult.Value;
    Console.WriteLine($"Current: {info.Current}");           // "light" or "dark"
    Console.WriteLine($"Effective: {info.Effective}");       // Resolved theme
    Console.WriteLine($"User Pref: {info.UserPreference}");  // "light", "dark", or "auto"
    Console.WriteLine($"System: {info.SystemTheme}");        // OS preference
    Console.WriteLine($"Is Dark: {info.IsDark}");            // boolean
    Console.WriteLine($"Is Auto: {info.IsAuto}");            // boolean
}
```

### Component-Level Theme Override

Apply a specific theme to an individual component:

```razor
<div @ref="_myDiv" class="my-component">
    This div will have its own theme
</div>

@code {
    private ElementReference _myDiv;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            // Force this component to always use dark theme
            await ThemeService.ApplyThemeToElementAsync(_myDiv, Theme.Dark);
        }
    }
}
```

Remove theme override:

```csharp
await ThemeService.RemoveThemeFromElementAsync(_myDiv);
```

### React to Theme Changes

```razor
@inject IThemeService ThemeService
@implements IDisposable

<p>Current theme: @_currentTheme</p>

@code {
    private string _currentTheme = "light";

    protected override async Task OnInitializedAsync()
    {
        // Subscribe to theme change events
        ThemeService.ThemeChanged += HandleThemeChanged;

        // Get initial theme
        var infoResult = await ThemeService.GetThemeInfoAsync();
        if (infoResult.IsSuccess)
        {
            _currentTheme = infoResult.Value.Current;
        }
    }

    private void HandleThemeChanged(object? sender, ThemeChangedEventArgs e)
    {
        _currentTheme = e.Theme;
        InvokeAsync(StateHasChanged);
    }

    public void Dispose()
    {
        ThemeService.ThemeChanged -= HandleThemeChanged;
    }
}
```

---

## CSS Theme Variables

### Surface Colors

```css
var(--surface-primary)    /* Main background */
var(--surface-secondary)  /* Secondary background */
var(--surface-tertiary)   /* Tertiary background */
var(--surface-inverse)    /* Inverse background (e.g., tooltips) */
```

### Text Colors

```css
var(--text-primary)       /* Primary text */
var(--text-secondary)     /* Secondary text */
var(--text-tertiary)      /* Tertiary text */
var(--text-disabled)      /* Disabled text */
var(--text-inverse)       /* Inverse text (on dark bg) */
var(--text-link)          /* Link color */
var(--text-link-hover)    /* Link hover color */
```

### Border Colors

```css
var(--border-default)     /* Default border */
var(--border-hover)       /* Hover border */
var(--border-focus)       /* Focus border */
var(--border-error)       /* Error border */
```

### Usage in Components

```razor
<div style="background: var(--surface-primary); color: var(--text-primary);">
    <p style="color: var(--text-secondary);">Secondary text</p>
    <button style="border: 1px solid var(--border-default);">Click me</button>
</div>
```

Or use CSS classes:

```css
.my-card {
    background: var(--surface-secondary);
    color: var(--text-primary);
    border: 1px solid var(--border-default);
    border-radius: var(--rounded-lg);
    padding: var(--space-4);
}
```

---

## Advanced Features

### Custom Theme Transition Duration

Modify `DropBearThemeManager.module.js`:

```javascript
constructor() {
    // ...
    this.transitionDuration = 500; // Change from 300ms to 500ms
}
```

### Disable Transitions for Specific Elements

```css
.no-theme-transition,
.no-theme-transition * {
    transition: none !important;
}
```

```html
<div class="no-theme-transition">
    This won't animate when theme changes
</div>
```

### Print Styles

The system automatically uses light theme for printing:

```css
@media print {
    :root, [data-theme] {
        color-scheme: light;
        --surface-primary: #ffffff !important;
        --text-primary: #000000 !important;
    }
}
```

### High Contrast Mode

Automatically enhanced contrast when OS is in high contrast mode:

```css
@media (prefers-contrast: high) {
    [data-theme="light"] {
        --text-primary: #000000;
        --surface-primary: #ffffff;
        --border-default: #000000;
    }

    [data-theme="dark"] {
        --text-primary: #ffffff;
        --surface-primary: #000000;
        --border-default: #ffffff;
    }
}
```

---

## Accessibility

### Reduced Motion

Users with `prefers-reduced-motion: reduce` automatically get instant (1ms) transitions:

```css
@media (prefers-reduced-motion: reduce) {
    .theme-transitioning,
    .theme-transitioning * {
        transition-duration: 1ms !important;
        animation-duration: 1ms !important;
    }
}
```

### Keyboard Accessibility

The `ThemeToggle` component is fully keyboard accessible:

- **Tab**: Focus the toggle button
- **Enter/Space**: Toggle theme
- **Screen reader**: Announces current theme state

```html
<button aria-label="Switch to dark mode" title="Switch to dark mode">
    <!-- Icon -->
</button>
```

### Focus Indicators

Focus states are always visible:

```css
*:focus-visible {
    outline: 2px solid var(--border-focus);
    outline-offset: 2px;
}

/* High contrast mode gets thicker outlines */
@media (prefers-contrast: high) {
    *:focus-visible {
        outline-width: 3px;
        outline-offset: 3px;
    }
}
```

---

## Troubleshooting

### Theme Not Persisting Across Sessions

**Cause**: localStorage might be disabled or unavailable

**Solution**: Check browser console for errors:

```javascript
try {
    localStorage.setItem('test', 'test');
    localStorage.removeItem('test');
    console.log('localStorage is available');
} catch (e) {
    console.error('localStorage is not available:', e);
}
```

### FOUC (Flash of Unstyled Content)

**Cause**: Theme not applied before render

**Solution**: The CSS includes FOUC prevention:

```css
html:not([data-theme]) {
    visibility: hidden;
}

html[data-theme] {
    visibility: visible;
}
```

Ensure `ThemeService.InitializeAsync()` is called early in app startup.

### Multi-Tab Sync Not Working

**Cause**: `storage` event only fires in OTHER tabs, not the current tab

**Behavior**: This is expected. The current tab applies the theme immediately, and the `storage` event propagates the change to other tabs.

### JavaScript Module Not Loading

**Error**: `Failed to load module script`

**Solution**: Verify the module path in `ThemeService.cs`:

```csharp
private const string ModulePath = "./js/DropBearThemeManager.module.js";
```

Ensure the file exists at `wwwroot/js/DropBearThemeManager.module.js`.

### Theme Variables Not Working

**Cause**: CSS not loaded or incorrect order

**Solution**: Load CSS in this order:

1. `design-system-modern.css` (defines variables)
2. `theme-transitions.css` (defines theme overrides)
3. Your component CSS (uses variables)

---

## Browser Support

| Feature | Chrome | Firefox | Safari | Edge |
|---------|--------|---------|--------|------|
| prefers-color-scheme | 76+ | 67+ | 12.1+ | 79+ |
| CSS Custom Properties | 49+ | 31+ | 9.1+ | 15+ |
| localStorage | ✅ | ✅ | ✅ | ✅ |
| ES6 Modules | 61+ | 60+ | 11+ | 79+ |

**Fallback**: If `prefers-color-scheme` is unsupported, defaults to light theme.

---

## Performance

### JavaScript Module

- **Singleton pattern**: Single instance per page
- **Lazy initialization**: Only initializes when first used
- **Debounced storage**: Prevents excessive localStorage writes
- **Efficient selectors**: Uses `document.documentElement` for theme attribute

### CSS Transitions

- **Hardware-accelerated**: Uses `transform`, `opacity` where possible
- **Transitioned properties**: Only color-related properties (not layout)
- **Removed after animation**: Transition class removed after 300ms

### Memory Usage

- **Small footprint**: ~10KB JavaScript (minified)
- **Minimal overhead**: ~2KB CSS
- **No dependencies**: Pure vanilla JavaScript

---

## Testing

### Unit Testing (bUnit)

```csharp
[Fact]
public async Task ThemeToggle_ShouldChangeTheme()
{
    // Arrange
    var ctx = new TestContext();
    var themeService = new ThemeService(ctx.JSInterop.JSRuntime, logger);
    ctx.Services.AddSingleton<IThemeService>(themeService);

    // Act
    var cut = ctx.RenderComponent<ThemeToggle>();
    var button = cut.Find("button");
    await button.ClickAsync(new MouseEventArgs());

    // Assert
    var result = await themeService.GetThemeInfoAsync();
    Assert.True(result.IsSuccess);
}
```

### Manual Testing Checklist

- [ ] Theme persists after page reload
- [ ] System theme changes are detected
- [ ] Smooth transitions work (if not reduced motion)
- [ ] Multi-tab sync works correctly
- [ ] Component-level overrides work
- [ ] High contrast mode enhances visibility
- [ ] Print preview uses light theme
- [ ] Keyboard navigation works
- [ ] Screen reader announces theme changes
- [ ] No FOUC on initial load

---

## Migration Guide

### From Legacy Theme System

If you have an existing theme system:

1. **Remove old theme JavaScript**:
   ```html
   <!-- DELETE -->
   <script src="old-theme.js"></script>
   ```

2. **Update CSS variable names**:
   ```css
   /* Old */
   background: var(--bg-primary);
   color: var(--fg-primary);

   /* New */
   background: var(--surface-primary);
   color: var(--text-primary);
   ```

3. **Replace theme toggle logic**:
   ```razor
   @* Old *@
   <button onclick="oldToggleTheme()">Toggle</button>

   @* New *@
   <ThemeToggle />
   ```

4. **Update service registration**:
   ```csharp
   // Old
   builder.Services.AddSingleton<IOldThemeService, OldThemeService>();

   // New
   builder.Services.AddSingleton<IThemeService, ThemeService>();
   ```

---

## API Reference

### IThemeService

```csharp
public interface IThemeService : IAsyncDisposable
{
    // Events
    event EventHandler<ThemeChangedEventArgs>? ThemeChanged;

    // Properties
    bool IsInitialized { get; }

    // Methods
    ValueTask<Result<ThemeInfo, JsInteropError>> InitializeAsync(
        CancellationToken cancellationToken = default);

    ValueTask<Result<bool, JsInteropError>> SetThemeAsync(
        Theme theme,
        bool animated = true,
        CancellationToken cancellationToken = default);

    ValueTask<Result<Theme, JsInteropError>> ToggleThemeAsync(
        bool animated = true,
        CancellationToken cancellationToken = default);

    ValueTask<Result<ThemeInfo, JsInteropError>> GetThemeInfoAsync(
        CancellationToken cancellationToken = default);

    ValueTask<Result<bool, JsInteropError>> ApplyThemeToElementAsync(
        ElementReference elementReference,
        Theme theme,
        CancellationToken cancellationToken = default);

    ValueTask<Result<bool, JsInteropError>> RemoveThemeFromElementAsync(
        ElementReference elementReference,
        CancellationToken cancellationToken = default);
}
```

### Theme Enum

```csharp
public enum Theme
{
    Light,  // Force light theme
    Dark,   // Force dark theme
    Auto    // Follow system preference
}
```

### ThemeInfo

```csharp
public sealed class ThemeInfo
{
    public required string Current { get; init; }          // "light" or "dark"
    public required string Effective { get; init; }        // Resolved theme
    public required string UserPreference { get; init; }   // "light", "dark", "auto"
    public required string SystemTheme { get; init; }      // OS preference
    public required bool IsAuto { get; init; }             // Using auto mode?
    public required bool IsDark { get; init; }             // Currently dark?
    public required bool IsLight { get; init; }            // Currently light?
}
```

### ThemeChangedEventArgs

```csharp
public sealed class ThemeChangedEventArgs : EventArgs
{
    public string Theme { get; }          // New theme
    public string? PreviousTheme { get; } // Previous theme (if any)
    public string UserPreference { get; } // User's preference setting
    public bool Animated { get; }         // Was transition animated?
}
```

---

## Resources

### Files Reference

| File | Purpose | Location |
|------|---------|----------|
| `DropBearThemeManager.module.js` | JavaScript theme manager | `wwwroot/js/` |
| `theme-transitions.css` | Theme transition styles | `wwwroot/styles/` |
| `design-system-modern.css` | Design tokens and variables | `wwwroot/styles/` |
| `ThemeService.cs` | Blazor C# service | `Services/` |
| `IThemeService.cs` | Service interface | `Interfaces/` |
| `ThemeToggle.razor` | Toggle button component | `Components/Buttons/` |
| `Theme.cs` | Theme enum | `Models/` |
| `ThemeInfo.cs` | Theme information model | `Models/` |
| `ThemeChangedEventArgs.cs` | Event args | `Models/` |

### Related Documentation

- [Design System Guide](./DESIGN_SYSTEM.md)
- [Accessibility Audit](./ACCESSIBILITY_AUDIT.md)
- [Component Library Overview](./README.md)

### External References

- [CSS Custom Properties (MDN)](https://developer.mozilla.org/en-US/docs/Web/CSS/Using_CSS_custom_properties)
- [prefers-color-scheme (MDN)](https://developer.mozilla.org/en-US/docs/Web/CSS/@media/prefers-color-scheme)
- [localStorage (MDN)](https://developer.mozilla.org/en-US/docs/Web/API/Window/localStorage)
- [WCAG 2.1 Guidelines](https://www.w3.org/WAI/WCAG21/quickref/)

---

**Version**: 1.0.0
**Last Updated**: 2025-10-30
**Maintainer**: DropBear.Codex Team
