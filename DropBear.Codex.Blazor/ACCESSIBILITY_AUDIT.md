# Blazor Components Accessibility Audit (Priority 1.2)

**Date**: 2025-10-30
**Status**: In Progress
**Target**: WCAG 2.1 Level AA Compliance

---

## Executive Summary

The DropBear.Codex.Blazor component library demonstrates **strong accessibility foundations** with comprehensive ARIA markup already implemented. This audit identifies remaining gaps and provides an implementation roadmap for achieving WCAG 2.1 Level AA compliance.

### Current State: ✅ Good Foundation
- Semantic HTML structure
- ARIA roles and labels throughout
- Live regions for dynamic content
- Screen reader friendly markup
- Keyboard-friendly buttons with aria-label

### Required Improvements: 🔧 Keyboard Navigation & Focus Management
- DataGrid arrow key navigation
- SelectionPanel keyboard interaction
- Modal focus trap and restoration
- Skip links for main content areas

---

## Component-by-Component Assessment

### 1. DropBearDataGrid ⚠️ **Needs Enhancement**

**Current Accessibility (Good)**:
- ✅ `role="status"`, `aria-live="polite"` for loading states
- ✅ `role="alert"`, `aria-live="assertive"` for errors
- ✅ Search input with `aria-label` and `aria-describedby`
- ✅ Buttons with proper `aria-label` attributes
- ✅ `class="sr-only"` for screen reader only labels

**Missing Features**:
- ❌ Table role and ARIA grid pattern
- ❌ Arrow key navigation (Up/Down/Left/Right)
- ❌ Page Up/Page Down for pagination
- ❌ Home/End for first/last row
- ❌ Enter/Space for row actions
- ❌ Focus indicator for current cell
- ❌ `aria-rowcount`, `aria-colcount` for screen readers
- ❌ `aria-sort` for sortable columns

**Recommended Implementation**:
```razor
<table role="grid"
       aria-rowcount="@TotalRows"
       aria-colcount="@_columns.Count"
       aria-label="@Title"
       @onkeydown="HandleGridKeyNavigation"
       tabindex="0">
    <thead>
        <tr role="row">
            @foreach (var column in _columns)
            {
                <th role="columnheader"
                    aria-sort="@GetAriaSortValue(column)"
                    tabindex="@(column.IsSortable ? 0 : -1)">
                    @column.Title
                </th>
            }
        </tr>
    </thead>
    <tbody>
        @foreach (var (item, rowIndex) in FilteredData.Select((x, i) => (x, i)))
        {
            <tr role="row"
                aria-rowindex="@(rowIndex + 1)"
                @onclick="() => SelectRow(item)"
                tabindex="@(rowIndex == _focusedRowIndex ? 0 : -1)"
                @onkeydown="@((e) => HandleRowKeyDown(e, item, rowIndex))">
                @foreach (var (column, colIndex) in _columns.Select((x, i) => (x, i)))
                {
                    <td role="gridcell"
                        aria-colindex="@(colIndex + 1)"
                        tabindex="-1">
                        @column.Template(item)
                    </td>
                }
            </tr>
        }
    </tbody>
</table>
```

**Keyboard Navigation Requirements**:
- Arrow Up/Down: Move between rows
- Arrow Left/Right: Move between columns
- Tab: Move to next interactive element
- Shift+Tab: Move to previous interactive element
- Enter/Space: Activate current row/cell
- Home: First column in current row
- End: Last column in current row
- Ctrl+Home: First cell in grid
- Ctrl+End: Last cell in grid
- Page Up/Down: Navigate pages if paginated

**WCAG Criteria Addressed**:
- 2.1.1 Keyboard (Level A)
- 2.1.3 Keyboard (No Exception) (Level AAA - optional)
- 2.4.3 Focus Order (Level A)
- 2.4.7 Focus Visible (Level AA)

---

### 2. DropBearSelectionPanel ⚠️ **Needs Enhancement**

**Current Accessibility (Very Good)**:
- ✅ `role="region"`, `role="list"`, `role="listitem"`
- ✅ `role="group"` for action buttons
- ✅ `aria-labelledby` for panel title
- ✅ `aria-hidden` for collapsed state
- ✅ `aria-expanded`, `aria-controls` for toggle button
- ✅ `aria-live="polite"` for selection count
- ✅ Individual `aria-label` for remove buttons

**Missing Features**:
- ❌ Keyboard navigation for list items (Tab, Arrow keys)
- ❌ Enter/Space to remove items
- ❌ Focus management when items are removed
- ❌ Delete key support for focused items
- ❌ Keyboard shortcuts (e.g., Shift+Delete to clear all)

**Recommended Implementation**:
```razor
<li class="selection-item"
    role="listitem"
    tabindex="0"
    @onkeydown="@((e) => HandleListItemKeyDown(e, item))"
    @ref="@GetItemRef(item)">
    <div class="item-content">
        @itemDisplay
    </div>
    <button type="button"
            class="remove-button"
            @onclick="@(() => RemoveItem(item))"
            @onkeydown="@((e) => HandleRemoveKeyDown(e, item))"
            aria-label="Remove @itemDisplay">
        ×
    </button>
</li>
```

**Keyboard Navigation Requirements**:
- Tab: Focus next list item
- Shift+Tab: Focus previous list item
- Arrow Up/Down: Navigate list items
- Enter/Space: Focus remove button
- Delete: Remove focused item
- Escape: Close panel if expanded

**Code Implementation** (in code-behind):
```csharp
private async Task HandleListItemKeyDown(KeyboardEventArgs e, T item)
{
    switch (e.Key)
    {
        case "Delete":
            await RemoveItem(item);
            await FocusNextItem();
            break;
        case "Enter":
        case " ": // Space
            await FocusRemoveButton(item);
            break;
        case "ArrowUp":
            e.PreventDefault();
            await FocusPreviousItem();
            break;
        case "ArrowDown":
            e.PreventDefault();
            await FocusNextItem();
            break;
    }
}
```

**WCAG Criteria Addressed**:
- 2.1.1 Keyboard (Level A)
- 2.4.3 Focus Order (Level A)
- 3.2.1 On Focus (Level A)

---

### 3. DropBearModalContainer ⚠️ **Needs Enhancement**

**Current Accessibility (Good)**:
- ✅ `role="dialog"`, `aria-modal="true"`
- ✅ `aria-labelledby` for modal title
- ✅ `@onkeydown` handler for keyboard events
- ✅ Overlay with click-to-close

**Missing Features**:
- ❌ Focus trap (Tab should cycle within modal)
- ❌ Focus restoration (return focus to trigger on close)
- ❌ Focus first focusable element when opened
- ❌ Escape key to close (may be implemented in HandleKeyDown)
- ❌ Announcement of modal opening for screen readers

**Recommended Implementation**:

**Markup**:
```razor
@if (ModalService.IsModalVisible)
{
    <div class="modal-overlay"
         @onclick="HandleOutsideClick"
         @onkeydown="HandleKeyDown"
         role="dialog"
         aria-modal="true"
         aria-labelledby="modal-title-@ComponentId"
         aria-describedby="modal-description-@ComponentId"
         tabindex="-1"
         @ref="_modalOverlay">

        <!-- Focus trap start -->
        <span tabindex="0" @onfocus="FocusLastElement"></span>

        <div class="modal-content @_modalTransitionClass"
             id="modal-content-@ComponentId"
             @onclick:stopPropagation="true"
             @ref="_modalContent">
            <DynamicComponent Type="ModalService.CurrentComponent"
                              Parameters="ChildParameters"/>
        </div>

        <!-- Focus trap end -->
        <span tabindex="0" @onfocus="FocusFirstElement"></span>
    </div>

    <!-- Screen reader announcement -->
    <div role="status" aria-live="assertive" class="sr-only">
        Modal dialog opened
    </div>
}
```

**Code Implementation**:
```csharp
private ElementReference _modalOverlay;
private ElementReference _modalContent;
private ElementReference? _previouslyFocusedElement;
private List<ElementReference> _focusableElements = new();

protected override async Task OnAfterRenderAsync(bool firstRender)
{
    if (ModalService.IsModalVisible && !_isModalFocused)
    {
        // Store currently focused element
        _previouslyFocusedElement = await JS.InvokeAsync<ElementReference>(
            "document.activeElement");

        // Get all focusable elements within modal
        _focusableElements = await GetFocusableElements(_modalContent);

        // Focus first element
        if (_focusableElements.Any())
        {
            await _focusableElements.First().FocusAsync();
        }
        else
        {
            await _modalOverlay.FocusAsync();
        }

        _isModalFocused = true;
    }
}

private async Task HandleKeyDown(KeyboardEventArgs e)
{
    if (e.Key == "Escape")
    {
        await CloseModalAsync();
    }
    else if (e.Key == "Tab")
    {
        await HandleTabNavigation(e);
    }
}

private async Task HandleTabNavigation(KeyboardEventArgs e)
{
    if (!_focusableElements.Any()) return;

    var currentIndex = await GetCurrentFocusIndex();

    if (e.ShiftKey)
    {
        // Shift+Tab: Move to previous element
        var newIndex = currentIndex <= 0
            ? _focusableElements.Count - 1
            : currentIndex - 1;
        await _focusableElements[newIndex].FocusAsync();
    }
    else
    {
        // Tab: Move to next element
        var newIndex = currentIndex >= _focusableElements.Count - 1
            ? 0
            : currentIndex + 1;
        await _focusableElements[newIndex].FocusAsync();
    }
}

private async Task CloseModalAsync()
{
    await ModalService.CloseAsync();

    // Restore focus to previously focused element
    if (_previouslyFocusedElement != null)
    {
        await _previouslyFocusedElement.Value.FocusAsync();
    }

    _isModalFocused = false;
}

private async Task<List<ElementReference>> GetFocusableElements(ElementReference container)
{
    // Query for all focusable elements
    return await JS.InvokeAsync<List<ElementReference>>(
        "getFocusableElements",
        container);
}
```

**JavaScript Helper** (in modal.js):
```javascript
export function getFocusableElements(container) {
    const selector = 'button, [href], input, select, textarea, [tabindex]:not([tabindex="-1"])';
    const elements = container.querySelectorAll(selector);
    return Array.from(elements).filter(el => {
        return !el.disabled && el.offsetParent !== null;
    });
}
```

**WCAG Criteria Addressed**:
- 2.1.2 No Keyboard Trap (Level A)
- 2.4.3 Focus Order (Level A)
- 3.2.1 On Focus (Level A)

---

## Additional Global Improvements

### 1. Skip Links

**Implementation** (in MainLayout.razor or _Host.cshtml):
```razor
<a href="#main-content" class="skip-link">Skip to main content</a>
<a href="#navigation" class="skip-link">Skip to navigation</a>

<nav id="navigation">
    <!-- Navigation content -->
</nav>

<main id="main-content" tabindex="-1">
    <!-- Main content -->
</main>
```

**CSS**:
```css
.skip-link {
    position: absolute;
    top: -40px;
    left: 0;
    background: var(--color-primary);
    color: white;
    padding: 8px;
    text-decoration: none;
    z-index: 100;
}

.skip-link:focus {
    top: 0;
}
```

### 2. Focus Visible Styling

**Global CSS Enhancement**:
```css
/* Ensure focus indicators are always visible */
*:focus-visible {
    outline: 2px solid var(--color-primary);
    outline-offset: 2px;
}

/* High contrast mode support */
@media (prefers-contrast: high) {
    *:focus-visible {
        outline: 3px solid currentColor;
        outline-offset: 3px;
    }
}

/* Reduced motion support */
@media (prefers-reduced-motion: reduce) {
    * {
        animation-duration: 0.01ms !important;
        animation-iteration-count: 1 !important;
        transition-duration: 0.01ms !important;
    }
}
```

### 3. Screen Reader Only Utility Class

**Verify Existence** (should already exist in `utilities.css`):
```css
.sr-only {
    position: absolute;
    width: 1px;
    height: 1px;
    padding: 0;
    margin: -1px;
    overflow: hidden;
    clip: rect(0, 0, 0, 0);
    white-space: nowrap;
    border-width: 0;
}

.sr-only-focusable:focus {
    position: static;
    width: auto;
    height: auto;
    overflow: visible;
    clip: auto;
    white-space: normal;
}
```

---

## Implementation Roadmap

### Phase 1: DataGrid Keyboard Navigation (2-3 hours)
1. Add grid role and ARIA attributes
2. Implement arrow key navigation
3. Implement Home/End navigation
4. Add focus indicators
5. Test with screen readers

### Phase 2: SelectionPanel Keyboard Support (1-2 hours)
1. Add keyboard event handlers
2. Implement arrow navigation
3. Add Delete key support
4. Focus management on item removal
5. Test keyboard-only workflow

### Phase 3: Modal Focus Trap (2-3 hours)
1. Implement focus trap logic
2. Store and restore previous focus
3. Auto-focus first element
4. Handle Tab/Shift+Tab cycling
5. Test with multiple modals

### Phase 4: Global Improvements (1 hour)
1. Add skip links to layout
2. Enhance focus visible styles
3. Verify sr-only utilities
4. Add reduced motion support
5. Test entire application flow

**Total Estimated Effort**: 6-9 hours

---

## Testing Checklist

### Automated Testing
- [ ] Axe DevTools (Chrome extension)
- [ ] WAVE (Web Accessibility Evaluation Tool)
- [ ] Lighthouse accessibility audit
- [ ] Pa11y CI integration

### Manual Testing

**Keyboard Navigation**:
- [ ] Can navigate entire app with Tab key only
- [ ] Focus indicators always visible
- [ ] No keyboard traps
- [ ] Logical tab order
- [ ] Arrow keys work in grid/lists
- [ ] Enter/Space activate buttons
- [ ] Escape closes modals

**Screen Readers**:
- [ ] NVDA (Windows - free)
- [ ] JAWS (Windows - commercial)
- [ ] VoiceOver (macOS - built-in)
- [ ] TalkBack (Android - built-in)

**Color/Contrast**:
- [ ] All text meets 4.5:1 contrast (AA)
- [ ] Large text meets 3:1 contrast (AA)
- [ ] Interactive elements meet 3:1 contrast
- [ ] Works in high contrast mode

**Responsive/Zoom**:
- [ ] Works at 200% zoom
- [ ] Works at 400% zoom (Level AAA)
- [ ] No horizontal scrolling
- [ ] Content reflows properly

---

## WCAG 2.1 Compliance Matrix

| Criterion | Level | Status | Notes |
|-----------|-------|--------|-------|
| 1.1.1 Non-text Content | A | ✅ | Icons have aria-hidden, images need alt |
| 1.3.1 Info and Relationships | A | ✅ | Semantic HTML, ARIA roles |
| 1.4.3 Contrast (Minimum) | AA | 🔧 | Needs audit |
| 2.1.1 Keyboard | A | 🔧 | Grid/Panel need enhancement |
| 2.1.2 No Keyboard Trap | A | 🔧 | Modal needs focus trap |
| 2.4.3 Focus Order | A | ✅ | Logical order |
| 2.4.7 Focus Visible | AA | 🔧 | Needs enhancement |
| 3.2.1 On Focus | A | ✅ | No unexpected behavior |
| 3.3.2 Labels or Instructions | A | ✅ | Good labels throughout |
| 4.1.2 Name, Role, Value | A | ✅ | ARIA markup present |

**Legend**: ✅ Compliant | 🔧 Needs Work | ❌ Not Compliant

---

## Resources

### Tools
- [Axe DevTools](https://www.deque.com/axe/devtools/)
- [WAVE](https://wave.webaim.org/)
- [Pa11y](https://pa11y.org/)
- [Lighthouse](https://developers.google.com/web/tools/lighthouse)
- [Color Contrast Analyzer](https://www.tpgi.com/color-contrast-checker/)

### Documentation
- [WCAG 2.1 Guidelines](https://www.w3.org/WAI/WCAG21/quickref/)
- [ARIA Authoring Practices Guide](https://www.w3.org/WAI/ARIA/apg/)
- [MDN Accessibility](https://developer.mozilla.org/en-US/docs/Web/Accessibility)
- [WebAIM Resources](https://webaim.org/resources/)

### Blazor Specific
- [Microsoft Accessibility Guidelines](https://docs.microsoft.com/en-us/aspnet/core/blazor/accessibility)
- [Blazor ARIA Patterns](https://github.com/microsoft/fluentui-blazor)

---

**Next Actions**: Begin Phase 1 implementation with DropBearDataGrid keyboard navigation.
