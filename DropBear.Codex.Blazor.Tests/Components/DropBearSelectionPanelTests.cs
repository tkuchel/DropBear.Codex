#region

using Bunit;
using DropBear.Codex.Blazor.Components.Panels;
using DropBear.Codex.Blazor.Models;
using DropBear.Codex.Blazor.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Xunit;

#endregion

namespace DropBear.Codex.Blazor.Tests.Components;

/// <summary>
///     Tests for the DropBearSelectionPanel component.
/// </summary>
public sealed class DropBearSelectionPanelTests : ComponentTestBase
{
    [Fact]
    public void SelectionPanel_Should_Render_WithTitle()
    {
        // Arrange
        const string title = "Selected Items";

        // Act
        var cut = RenderComponent<DropBearSelectionPanel<string>>(parameters => parameters
            .Add(p => p.Title, title));

        // Assert
        var titleElement = cut.Find(".panel-title");
        titleElement.TextContent.Should().Be(title);
    }

    [Fact]
    public void SelectionPanel_Should_DisplayEmptyMessage_WhenNoItemsSelected()
    {
        // Arrange
        const string emptyMessage = "No items selected";

        // Act
        var cut = RenderComponent<DropBearSelectionPanel<string>>(parameters => parameters
            .Add(p => p.EmptySelectionText, emptyMessage));

        // Assert
        var emptyElement = cut.Find(".empty-selection");
        emptyElement.TextContent.Should().Contain(emptyMessage);
    }

    [Fact]
    public void SelectionPanel_Should_DisplaySelectedItemCount()
    {
        // Arrange
        var selectedItems = new List<string> { "Item1", "Item2", "Item3" };

        // Act
        var cut = RenderComponent<DropBearSelectionPanel<string>>(parameters => parameters
            .Add(p => p.SelectedItems, selectedItems));

        // Assert
        var countElement = cut.Find(".selection-count");
        countElement.TextContent.Should().Contain("3 items selected");
    }

    [Fact]
    public void SelectionPanel_Should_RenderSelectedItems()
    {
        // Arrange
        var selectedItems = new List<string> { "Item1", "Item2", "Item3" };

        // Act
        var cut = RenderComponent<DropBearSelectionPanel<string>>(parameters => parameters
            .Add(p => p.SelectedItems, selectedItems));

        // Assert
        var listItems = cut.FindAll(".selection-item");
        listItems.Should().HaveCount(3);
    }

    [Fact]
    public void SelectionPanel_Should_UseItemDisplayExpression_WhenProvided()
    {
        // Arrange
        var selectedItems = new List<TestItem>
        {
            new() { Id = 1, Name = "First Item" },
            new() { Id = 2, Name = "Second Item" }
        };

        // Act
        var cut = RenderComponent<DropBearSelectionPanel<TestItem>>(parameters => parameters
            .Add(p => p.SelectedItems, selectedItems)
            .Add(p => p.ItemDisplayExpression, item => item.Name));

        // Assert
        var listItems = cut.FindAll(".selection-item");
        listItems[0].TextContent.Should().Contain("First Item");
        listItems[1].TextContent.Should().Contain("Second Item");
    }

    [Fact]
    public void SelectionPanel_Should_RenderCustomItemTemplate_WhenProvided()
    {
        // Arrange
        var selectedItems = new List<string> { "Item1" };
        RenderFragment<string> template = item => builder =>
        {
            builder.OpenElement(0, "div");
            builder.AddAttribute(1, "class", "custom-item");
            builder.AddContent(2, $"Custom: {item}");
            builder.CloseElement();
        };

        // Act
        var cut = RenderComponent<DropBearSelectionPanel<string>>(parameters => parameters
            .Add(p => p.SelectedItems, selectedItems)
            .Add(p => p.ItemTemplate, template));

        // Assert
        var customItem = cut.Find(".custom-item");
        customItem.TextContent.Should().Contain("Custom: Item1");
    }

    [Fact]
    public async Task SelectionPanel_Should_RemoveItem_WhenRemoveButtonClicked()
    {
        // Arrange
        var selectedItems = new List<string> { "Item1", "Item2", "Item3" };
        var itemRemoved = false;
        string? removedItem = null;

        var cut = RenderComponent<DropBearSelectionPanel<string>>(parameters => parameters
            .Add(p => p.SelectedItems, selectedItems)
            .Add(p => p.OnItemRemoved, item =>
            {
                itemRemoved = true;
                removedItem = item;
            }));

        // Act
        var removeButtons = cut.FindAll(".remove-button");
        await removeButtons[0].ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        // Assert
        itemRemoved.Should().BeTrue();
        removedItem.Should().Be("Item1");
    }

    [Fact]
    public void SelectionPanel_Should_HaveAccessibleRemoveButton()
    {
        // Arrange
        var selectedItems = new List<string> { "TestItem" };

        // Act
        var cut = RenderComponent<DropBearSelectionPanel<string>>(parameters => parameters
            .Add(p => p.SelectedItems, selectedItems));

        // Assert
        var removeButton = cut.Find(".remove-button");
        removeButton.GetAttribute("aria-label").Should().Contain("Remove");
        removeButton.GetAttribute("type").Should().Be("button");
    }

    [Fact]
    public async Task SelectionPanel_Should_ToggleCollapse_WhenToggleButtonClicked()
    {
        // Arrange
        var selectedItems = new List<string> { "Item1" };
        var cut = RenderComponent<DropBearSelectionPanel<string>>(parameters => parameters
            .Add(p => p.SelectedItems, selectedItems)
            .Add(p => p.InitiallyCollapsed, false));

        // Act
        var initialState = cut.Instance.IsCollapsed;
        await cut.InvokeAsync(() => cut.Instance.ToggleCollapse());

        // Assert
        cut.Instance.IsCollapsed.Should().Be(!initialState);
    }

    [Fact]
    public void SelectionPanel_Should_HaveCorrectAriaAttributes_ForCollapsible()
    {
        // Arrange
        var selectedItems = new List<string> { "Item1" };

        // Act
        var cut = RenderComponent<DropBearSelectionPanel<string>>(parameters => parameters
            .Add(p => p.SelectedItems, selectedItems)
            .Add(p => p.InitiallyCollapsed, false));

        // Assert
        var toggleButton = cut.Find(".selection-panel-toggle");
        toggleButton.GetAttribute("aria-expanded").Should().Be("true");
        toggleButton.HasAttribute("aria-controls").Should().BeTrue();
        toggleButton.HasAttribute("aria-label").Should().BeTrue();
    }

    [Fact]
    public void SelectionPanel_Should_HideContent_WhenInitiallyCollapsed()
    {
        // Arrange
        var selectedItems = new List<string> { "Item1" };

        // Act
        var cut = RenderComponent<DropBearSelectionPanel<string>>(parameters => parameters
            .Add(p => p.SelectedItems, selectedItems)
            .Add(p => p.InitiallyCollapsed, true));

        // Assert
        var content = cut.Find(".selection-panel-content");
        content.ClassList.Should().Contain("collapsed");
        content.GetAttribute("aria-hidden").Should().Be("true");
    }

    [Fact]
    public void SelectionPanel_Should_RenderActionButtons_WhenProvided()
    {
        // Arrange
        var selectedItems = new List<string> { "Item1", "Item2" };
        var actionButtons = new List<ActionButton<string>>
        {
            new()
            {
                Text = "Delete All",
                IconClass = "fas fa-trash",
                ButtonClass = "btn-danger"
            },
            new()
            {
                Text = "Export",
                IconClass = "fas fa-download",
                ButtonClass = "btn-primary"
            }
        };

        // Act
        var cut = RenderComponent<DropBearSelectionPanel<string>>(parameters => parameters
            .Add(p => p.SelectedItems, selectedItems)
            .Add(p => p.ActionButtons, actionButtons));

        // Assert
        var buttons = cut.FindAll(".action-button");
        buttons.Should().HaveCount(2);
        buttons[0].TextContent.Should().Contain("Delete All");
        buttons[1].TextContent.Should().Contain("Export");
    }

    [Fact]
    public async Task SelectionPanel_Should_InvokeActionButton_WhenClicked()
    {
        // Arrange
        var selectedItems = new List<string> { "Item1", "Item2" };
        var buttonClicked = false;
        var actionButtons = new List<ActionButton<string>>
        {
            new()
            {
                Text = "Test Action",
                OnClick = EventCallback.Factory.Create<List<string>>(this, items =>
                {
                    buttonClicked = true;
                })
            }
        };

        var cut = RenderComponent<DropBearSelectionPanel<string>>(parameters => parameters
            .Add(p => p.SelectedItems, selectedItems)
            .Add(p => p.ActionButtons, actionButtons));

        // Act
        var button = cut.Find(".action-button");
        await button.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        // Assert
        buttonClicked.Should().BeTrue();
    }

    [Fact]
    public void SelectionPanel_Should_HandleNoItems_Gracefully()
    {
        // Arrange
        var actionButtons = new List<ActionButton<string>>
        {
            new()
            {
                Text = "Test Action",
                OnClick = EventCallback.Factory.Create<List<string>>(this, _ => { })
            }
        };

        // Act
        var cut = RenderComponent<DropBearSelectionPanel<string>>(parameters => parameters
            .Add(p => p.SelectedItems, new List<string>())
            .Add(p => p.ActionButtons, actionButtons));

        // Assert - Component should render without errors
        cut.Instance.Should().NotBeNull();
    }

    [Fact]
    public void SelectionPanel_Should_LimitActionButtons_ToMaxThree()
    {
        // Arrange
        var selectedItems = new List<string> { "Item1" };
        var actionButtons = new List<ActionButton<string>>
        {
            new() { Text = "Action 1" },
            new() { Text = "Action 2" },
            new() { Text = "Action 3" },
            new() { Text = "Action 4" },
            new() { Text = "Action 5" }
        };

        // Act
        var cut = RenderComponent<DropBearSelectionPanel<string>>(parameters => parameters
            .Add(p => p.SelectedItems, selectedItems)
            .Add(p => p.ActionButtons, actionButtons));

        // Assert
        var buttons = cut.FindAll(".action-button");
        buttons.Should().HaveCount(3);
    }

    [Fact]
    public void SelectionPanel_Should_HaveProperListSemantics()
    {
        // Arrange
        var selectedItems = new List<string> { "Item1", "Item2" };

        // Act
        var cut = RenderComponent<DropBearSelectionPanel<string>>(parameters => parameters
            .Add(p => p.SelectedItems, selectedItems));

        // Assert
        var list = cut.Find(".selection-list");
        list.GetAttribute("role").Should().Be("list");
        list.HasAttribute("aria-label").Should().BeTrue();

        var listItems = cut.FindAll(".selection-item");
        foreach (var item in listItems)
        {
            item.GetAttribute("role").Should().Be("listitem");
        }
    }

    [Fact]
    public void SelectionPanel_Should_DisposeCorrectly()
    {
        // Arrange
        var selectedItems = new List<string> { "Item1" };
        var cut = RenderComponent<DropBearSelectionPanel<string>>(parameters => parameters
            .Add(p => p.SelectedItems, selectedItems));

        // Act
        var exception = Record.Exception(() => cut.Dispose());

        // Assert
        exception.Should().BeNull();
    }

    /// <summary>
    ///     Test item for testing complex object selection.
    /// </summary>
    private class TestItem
    {
        public int Id { get; init; }
        public required string Name { get; init; }
    }
}
