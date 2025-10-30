# Modern Design System Documentation

## Overview

The DropBear.Codex.Blazor modern design system provides a comprehensive set of design tokens following industry best practices. The system is built to work alongside the existing design system, offering both backward compatibility and modern standards.

**File**: `design-system-modern.css`
**Version**: 1.0.0
**Last Updated**: 2025-10-30

---

## Quick Start

### Including the Design System

Add to your `_Host.cshtml` or `App.razor`:

```html
<link href="~/styles/design-system-modern.css" rel="stylesheet" />
```

### Basic Usage

```css
/* Using modern color scales */
.my-component {
  background-color: var(--color-primary-500);
  color: var(--text-primary);
  padding: var(--space-4);
  border-radius: var(--rounded-lg);
}

/* Responsive spacing */
@media (min-width: 768px) {
  .my-component {
    padding: var(--space-8);
  }
}
```

---

## Color System

### Color Scales (50-900)

All colors follow a consistent 11-step scale from lightest (50) to darkest (950):

#### Neutral Grays
```css
--color-gray-50    /* #f9fafb - Lightest */
--color-gray-500   /* #6b7280 - Mid-tone */
--color-gray-900   /* #111827 - Darkest */
```

#### Primary (Teal/Cyan)
```css
--color-primary-50    /* Lightest teal */
--color-primary-500   /* Base primary (#06b6d4) */
--color-primary-900   /* Darkest teal */
```

#### Success (Green)
```css
--color-success-500   /* Base success (#22c55e) */
```

#### Warning (Amber)
```css
--color-warning-500   /* Base warning (#f59e0b) */
```

#### Error (Red)
```css
--color-error-500     /* Base error (#ef4444) */
```

#### Info (Blue)
```css
--color-info-500      /* Base info (#3b82f6) */
```

#### Secondary (Indigo/Purple)
```css
--color-secondary-500 /* Base secondary (#6366f1) */
```

### Semantic Colors

The system provides semantic color mappings that automatically adapt to light/dark themes:

```css
/* Surface/Background */
--surface-primary      /* Main background */
--surface-secondary    /* Card/panel backgrounds */
--surface-tertiary     /* Nested content */
--surface-inverse      /* Contrasting background */

/* Text */
--text-primary         /* Main text */
--text-secondary       /* Supporting text */
--text-tertiary        /* De-emphasized text */
--text-disabled        /* Disabled state */
--text-inverse         /* Text on dark backgrounds */
--text-link            /* Link color */
--text-link-hover      /* Link hover state */

/* Borders */
--border-default       /* Standard borders */
--border-hover         /* Hover state */
--border-focus         /* Focus state */
--border-error         /* Error state */
```

### Usage Examples

```css
/* Card component */
.card {
  background-color: var(--surface-secondary);
  border: 1px solid var(--border-default);
  color: var(--text-primary);
}

.card:hover {
  border-color: var(--border-hover);
}

/* Button variants */
.btn-primary {
  background-color: var(--color-primary-500);
  color: white;
}

.btn-primary:hover {
  background-color: var(--color-primary-600);
}

.btn-success {
  background-color: var(--color-success-500);
  color: white;
}

.btn-danger {
  background-color: var(--color-error-500);
  color: white;
}
```

---

## Typography Scale

### Size Scale (Major Third - 1.25 Ratio)

The typography system uses a Major Third (1.25) scale for harmonious text sizing:

| Token | Size | Pixels | Use Case |
|-------|------|--------|----------|
| `--text-xs` | 0.75rem | 12px | Fine print, captions |
| `--text-sm` | 0.875rem | 14px | Small text, labels |
| `--text-base` | 1rem | 16px | Body text (base) |
| `--text-lg` | 1.125rem | 18px | Large body text |
| `--text-xl` | 1.25rem | 20px | Subheadings |
| `--text-2xl` | 1.5rem | 24px | Section headers |
| `--text-3xl` | 1.875rem | 30px | Page titles |
| `--text-4xl` | 2.25rem | 36px | Hero text |
| `--text-5xl` | 3rem | 48px | Display text |
| `--text-6xl` | 3.75rem | 60px | Large display |
| `--text-7xl` | 4.5rem | 72px | Extra large |
| `--text-8xl` | 6rem | 96px | Huge display |
| `--text-9xl` | 8rem | 128px | Maximum |

### Line Heights

```css
--leading-none: 1         /* Tight, for headings */
--leading-tight: 1.25     /* Slightly loose */
--leading-snug: 1.375     /* Comfortable */
--leading-normal: 1.5     /* Standard body text */
--leading-relaxed: 1.625  /* Spacious */
--leading-loose: 2        /* Maximum spacing */
```

### Letter Spacing

```css
--tracking-tighter: -0.05em   /* Very tight */
--tracking-tight: -0.025em    /* Tight */
--tracking-normal: 0em        /* Standard */
--tracking-wide: 0.025em      /* Wide */
--tracking-wider: 0.05em      /* Wider */
--tracking-widest: 0.1em      /* Widest */
```

### Font Weights

```css
--font-thin: 100          /* Thin */
--font-extralight: 200    /* Extra Light */
--font-light: 300         /* Light */
--font-normal: 400        /* Normal (default) */
--font-medium: 500        /* Medium */
--font-semibold: 600      /* Semi Bold */
--font-bold: 700          /* Bold */
--font-extrabold: 800     /* Extra Bold */
--font-black: 900         /* Black */
```

### Typography Examples

```css
/* Heading styles */
h1 {
  font-size: var(--text-5xl);
  font-weight: var(--font-bold);
  line-height: var(--leading-tight);
  letter-spacing: var(--tracking-tight);
}

h2 {
  font-size: var(--text-3xl);
  font-weight: var(--font-semibold);
  line-height: var(--leading-snug);
}

/* Body text */
.body-text {
  font-size: var(--text-base);
  line-height: var(--leading-normal);
}

/* Small text */
.caption {
  font-size: var(--text-sm);
  color: var(--text-secondary);
  line-height: var(--leading-relaxed);
}
```

---

## Spacing System

### 4px Base Scale

The spacing system uses a 4px (0.25rem) base unit for consistent spacing:

| Token | Size | Pixels | Use Case |
|-------|------|--------|----------|
| `--space-0` | 0 | 0px | No spacing |
| `--space-px` | 1px | 1px | Hairline borders |
| `--space-0-5` | 0.125rem | 2px | Tiny spacing |
| `--space-1` | 0.25rem | 4px | Extra small |
| `--space-2` | 0.5rem | 8px | Small |
| `--space-3` | 0.75rem | 12px | Compact |
| `--space-4` | 1rem | 16px | Base spacing |
| `--space-6` | 1.5rem | 24px | Medium |
| `--space-8` | 2rem | 32px | Large |
| `--space-12` | 3rem | 48px | Extra large |
| `--space-16` | 4rem | 64px | Huge |
| `--space-24` | 6rem | 96px | Massive |
| `--space-32` | 8rem | 128px | Maximum |

Full scale includes: 0, px, 0.5, 1, 1.5, 2, 2.5, 3, 3.5, 4, 5, 6, 7, 8, 9, 10, 11, 12, 14, 16, 20, 24, 28, 32, 36, 40, 44, 48, 52, 56, 60, 64, 72, 80, 96

### Spacing Examples

```css
/* Component padding */
.card {
  padding: var(--space-6); /* 24px */
}

@media (min-width: 768px) {
  .card {
    padding: var(--space-8); /* 32px on larger screens */
  }
}

/* Margin between elements */
.stack > * + * {
  margin-top: var(--space-4); /* 16px */
}

/* Section spacing */
.section {
  padding-top: var(--space-16);    /* 64px */
  padding-bottom: var(--space-16);
}

/* Inline spacing */
.button-group > * + * {
  margin-left: var(--space-2); /* 8px */
}
```

---

## Responsive Breakpoints

### Breakpoint Values

```css
--breakpoint-sm: 640px    /* Small tablets */
--breakpoint-md: 768px    /* Medium tablets/landscape */
--breakpoint-lg: 1024px   /* Laptops/desktops */
--breakpoint-xl: 1280px   /* Large desktops */
--breakpoint-2xl: 1536px  /* Ultra-wide screens */
```

### Container Max-Widths

```css
--container-sm: 640px
--container-md: 768px
--container-lg: 1024px
--container-xl: 1280px
--container-2xl: 1536px
```

### Responsive Utilities

#### Container

Use `.container-modern` for responsive max-width containers:

```html
<div class="container-modern">
  <!-- Content automatically constrains to appropriate width -->
</div>
```

#### Media Query Usage

```css
/* Mobile first approach */
.my-component {
  padding: var(--space-4); /* Mobile: 16px */
}

@media (min-width: 640px) {
  .my-component {
    padding: var(--space-6); /* Tablet: 24px */
  }
}

@media (min-width: 1024px) {
  .my-component {
    padding: var(--space-8); /* Desktop: 32px */
  }
}
```

### Responsive Typography Example

```css
.hero-title {
  font-size: var(--text-3xl);
  line-height: var(--leading-tight);
}

@media (min-width: 768px) {
  .hero-title {
    font-size: var(--text-5xl);
  }
}

@media (min-width: 1024px) {
  .hero-title {
    font-size: var(--text-6xl);
  }
}
```

---

## Border Radius

### Scale

```css
--rounded-none: 0         /* No rounding */
--rounded-sm: 0.125rem    /* 2px - Subtle */
--rounded: 0.25rem        /* 4px - Default */
--rounded-md: 0.375rem    /* 6px - Medium */
--rounded-lg: 0.5rem      /* 8px - Large */
--rounded-xl: 0.75rem     /* 12px - Extra large */
--rounded-2xl: 1rem       /* 16px - Huge */
--rounded-3xl: 1.5rem     /* 24px - Maximum */
--rounded-full: 9999px    /* Fully rounded/circular */
```

### Examples

```css
/* Buttons */
.btn {
  border-radius: var(--rounded-lg); /* 8px */
}

/* Cards */
.card {
  border-radius: var(--rounded-xl); /* 12px */
}

/* Avatar / circular elements */
.avatar {
  border-radius: var(--rounded-full);
}

/* Pills/badges */
.badge {
  border-radius: var(--rounded-full);
}
```

---

## Box Shadows

### Shadow Scale

```css
--shadow-sm     /* Subtle shadow */
--shadow        /* Default shadow */
--shadow-md     /* Medium elevation */
--shadow-lg     /* Large elevation */
--shadow-xl     /* Extra large */
--shadow-2xl    /* Maximum elevation */
--shadow-inner  /* Inset shadow */
--shadow-none   /* No shadow */
```

### Shadow Examples

```css
/* Card with subtle shadow */
.card {
  box-shadow: var(--shadow-md);
}

.card:hover {
  box-shadow: var(--shadow-lg);
}

/* Floating action button */
.fab {
  box-shadow: var(--shadow-xl);
}

/* Input focus state */
.input:focus {
  box-shadow: 0 0 0 3px var(--color-primary-200);
}
```

---

## Animations & Transitions

### Duration

```css
--duration-75: 75ms      /* Extra fast */
--duration-100: 100ms    /* Fast */
--duration-150: 150ms    /* Quick (default) */
--duration-200: 200ms    /* Normal */
--duration-300: 300ms    /* Slow */
--duration-500: 500ms    /* Slower */
--duration-700: 700ms    /* Very slow */
--duration-1000: 1000ms  /* Extra slow */
```

### Easing Functions

```css
--ease-linear: linear                       /* Constant speed */
--ease-in: cubic-bezier(0.4, 0, 1, 1)      /* Slow start */
--ease-out: cubic-bezier(0, 0, 0.2, 1)     /* Slow end */
--ease-in-out: cubic-bezier(0.4, 0, 0.2, 1) /* Slow both ends */
```

### Pre-defined Transitions

```css
--transition-colors    /* Color transitions */
--transition-opacity   /* Opacity transitions */
--transition-shadow    /* Shadow transitions */
--transition-transform /* Transform transitions */
--transition-all       /* All properties */
```

### Animation Examples

```css
/* Button hover animation */
.btn {
  transition: var(--transition-colors);
}

.btn:hover {
  background-color: var(--color-primary-600);
}

/* Fade in animation */
.fade-in {
  animation: fadeIn var(--duration-300) var(--ease-out);
}

@keyframes fadeIn {
  from { opacity: 0; }
  to { opacity: 1; }
}

/* Transform animation */
.scale-up {
  transition: var(--transition-transform);
}

.scale-up:hover {
  transform: scale(1.05);
}
```

---

## Z-Index Scale

### Layering System

```css
--z-0: 0              /* Base layer */
--z-10: 10            /* Above base */
--z-20: 20            /* Slight elevation */
--z-30: 30            /* Medium elevation */
--z-40: 40            /* High elevation */
--z-50: 50            /* Highest standard */

/* Named layers for UI components */
--z-dropdown: 1000
--z-sticky: 1020
--z-fixed: 1030
--z-modal-backdrop: 1040
--z-modal: 1050
--z-popover: 1060
--z-tooltip: 1070
```

---

## Dark Mode Support

### Automatic Detection

The design system automatically adapts to the user's system preference:

```css
@media (prefers-color-scheme: dark) {
  /* Dark theme styles automatically applied */
}
```

### Manual Control

Use the `data-theme` attribute for manual theme control:

```html
<!-- Light theme -->
<html data-theme="light">

<!-- Dark theme -->
<html data-theme="dark">
```

### JavaScript Theme Toggle

```javascript
// Toggle theme
function toggleTheme() {
  const html = document.documentElement;
  const currentTheme = html.getAttribute('data-theme');
  const newTheme = currentTheme === 'dark' ? 'light' : 'dark';
  html.setAttribute('data-theme', newTheme);
  localStorage.setItem('theme', newTheme);
}

// Initialize theme from localStorage
const savedTheme = localStorage.getItem('theme');
if (savedTheme) {
  document.documentElement.setAttribute('data-theme', savedTheme);
}
```

---

## Accessibility Features

### Reduced Motion

The design system respects user motion preferences:

```css
@media (prefers-reduced-motion: reduce) {
  /* All animations reduced to near-instant */
}
```

### High Contrast

Automatic adjustments for high contrast mode:

```css
@media (prefers-contrast: high) {
  /* Enhanced contrast for borders and text */
}
```

### Focus States

Ensure keyboard navigation visibility:

```css
button:focus-visible {
  outline: 2px solid var(--border-focus);
  outline-offset: 2px;
}
```

---

## Migration from Legacy System

### Backward Compatibility

The modern design system works alongside the existing `variables.css`. You can use both:

```css
/* Legacy */
.old-component {
  color: var(--clr-primary);
  padding: var(--spacing-md);
}

/* Modern */
.new-component {
  color: var(--color-primary-500);
  padding: var(--space-6);
}
```

### Gradual Adoption

Recommended migration path:

1. **Include both stylesheets** (no breaking changes)
2. **New components**: Use modern tokens
3. **Refactor existing**: Gradually update as components are maintained
4. **Eventually deprecate**: Remove legacy system when ready

---

## Best Practices

### DO ✅

- Use semantic color tokens (`--surface-primary`, `--text-primary`) for theme compatibility
- Use the spacing scale consistently (multiples of 4px)
- Leverage responsive containers for layouts
- Use pre-defined transitions for consistency
- Respect user motion/contrast preferences

### DON'T ❌

- Hardcode pixel values (use tokens instead)
- Mix legacy and modern tokens inconsistently
- Override system colors without semantic mappings
- Ignore responsive breakpoints
- Use fixed z-index values (use the scale)

---

## Examples Gallery

### Card Component

```css
.modern-card {
  background-color: var(--surface-secondary);
  border: 1px solid var(--border-default);
  border-radius: var(--rounded-lg);
  padding: var(--space-6);
  box-shadow: var(--shadow-md);
  transition: var(--transition-shadow);
}

.modern-card:hover {
  box-shadow: var(--shadow-lg);
}
```

### Button Component

```css
.modern-btn {
  padding: var(--space-3) var(--space-6);
  font-size: var(--text-base);
  font-weight: var(--font-medium);
  border-radius: var(--rounded-lg);
  transition: var(--transition-colors);
}

.modern-btn-primary {
  background-color: var(--color-primary-500);
  color: white;
}

.modern-btn-primary:hover {
  background-color: var(--color-primary-600);
}
```

### Form Input

```css
.modern-input {
  padding: var(--space-3) var(--space-4);
  font-size: var(--text-base);
  background-color: var(--surface-primary);
  border: 1px solid var(--border-default);
  border-radius: var(--rounded-md);
  color: var(--text-primary);
  transition: var(--transition-colors);
}

.modern-input:focus {
  outline: none;
  border-color: var(--border-focus);
  box-shadow: 0 0 0 3px var(--color-primary-100);
}
```

---

## Resources

- [Tailwind CSS](https://tailwindcss.com/docs) - Inspiration for token naming
- [Material Design](https://material.io/design) - Color system principles
- [Refactoring UI](https://refactoringui.com/) - Design system best practices
- [WCAG 2.1 Guidelines](https://www.w3.org/WAI/WCAG21/quickref/) - Accessibility standards

---

**Last Updated**: 2025-10-30
**Version**: 1.0.0
**Maintainer**: DropBear.Codex Team
