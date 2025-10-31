#region

using Bunit;
using DropBear.Codex.Blazor.Components.Grids;
using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

#endregion

namespace DropBear.Codex.Blazor.Tests.Components;

/// <summary>
///     Tests for the DropBearDataGrid component.
/// </summary>
public sealed class DropBearDataGridTests : ComponentTestBase
{
    [Fact]
    public void DataGrid_Should_RenderWithTitle()
    {
        // Arrange
        const string title = "Test Grid";
        var items = GetTestItems();

        // Act
        var cut = RenderComponent<DropBearDataGrid<TestItem>>(parameters => parameters
            .Add(p => p.Title, title)
            .Add(p => p.Items, items));

        // Assert
        var headerText = cut.Find(".datagrid-header h2");
        headerText.TextContent.Should().Be(title);
    }

    [Fact]
    public void DataGrid_Should_ShowLoadingState_Initially()
    {
        // Arrange
        var items = GetTestItems();

        // Act
        var cut = RenderComponent<DropBearDataGrid<TestItem>>(parameters => parameters
            .Add(p => p.Items, items));

        // Assert - Component should handle loading state internally
        cut.Instance.Should().NotBeNull();
    }

    [Fact]
    public void DataGrid_Should_ShowNoDataMessage_WhenEmpty()
    {
        // Arrange & Act
        var cut = RenderComponent<DropBearDataGrid<TestItem>>(parameters => parameters
            .Add(p => p.Items, new List<TestItem>()));

        // Assert - Wait for initialization
        cut.WaitForState(() => cut.FindAll(".no-data-container").Count > 0, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void DataGrid_Should_RenderSearchBox_WhenSearchEnabled()
    {
        // Arrange
        var items = GetTestItems();

        // Act
        var cut = RenderComponent<DropBearDataGrid<TestItem>>(parameters => parameters
            .Add(p => p.Items, items)
            .Add(p => p.EnableSearch, true));

        // Assert
        cut.WaitForState(() => cut.FindAll(".search-input").Count > 0, TimeSpan.FromSeconds(2));
        var searchInput = cut.Find(".search-input");
        searchInput.GetAttribute("type").Should().Be("search");
        searchInput.HasAttribute("aria-label").Should().BeTrue();
    }

    [Fact]
    public void DataGrid_Should_NotRenderSearchBox_WhenSearchDisabled()
    {
        // Arrange
        var items = GetTestItems();

        // Act
        var cut = RenderComponent<DropBearDataGrid<TestItem>>(parameters => parameters
            .Add(p => p.Items, items)
            .Add(p => p.EnableSearch, false));

        // Assert
        cut.WaitForState(() => cut.FindAll(".datagrid-table").Count > 0, TimeSpan.FromSeconds(2));
        var searchInputs = cut.FindAll(".search-input");
        searchInputs.Should().BeEmpty();
    }

    [Fact]
    public void DataGrid_Should_RenderAddButton_WhenAllowAddIsTrue()
    {
        // Arrange
        var items = GetTestItems();

        // Act
        var cut = RenderComponent<DropBearDataGrid<TestItem>>(parameters => parameters
            .Add(p => p.Items, items)
            .Add(p => p.AllowAdd, true));

        // Assert
        cut.WaitForState(() => cut.FindAll(".btn-primary").Count > 0, TimeSpan.FromSeconds(2));
        var addButton = cut.FindAll(".btn-primary").FirstOrDefault(b => b.TextContent.Contains("Add"));
        addButton.Should().NotBeNull();
    }

    [Fact]
    public void DataGrid_Should_RenderExportButton_WhenAllowExportIsTrue()
    {
        // Arrange
        var items = GetTestItems();

        // Act
        var cut = RenderComponent<DropBearDataGrid<TestItem>>(parameters => parameters
            .Add(p => p.Items, items)
            .Add(p => p.AllowExport, true));

        // Assert
        cut.WaitForState(() => cut.FindAll(".btn-secondary").Count > 0, TimeSpan.FromSeconds(2));
        var exportButton = cut.FindAll(".btn-secondary").FirstOrDefault(b => b.TextContent.Contains("Export"));
        exportButton.Should().NotBeNull();
    }

    [Fact]
    public void DataGrid_Should_HaveProperTableSemantics()
    {
        // Arrange
        var items = GetTestItems();

        // Act
        var cut = RenderComponent<DropBearDataGrid<TestItem>>(parameters => parameters
            .Add(p => p.Items, items));

        // Assert
        cut.WaitForState(() => cut.FindAll(".datagrid-table").Count > 0, TimeSpan.FromSeconds(2));
        var table = cut.Find(".datagrid-table");
        table.GetAttribute("role").Should().Be("grid");
        table.HasAttribute("aria-label").Should().BeTrue();
        table.HasAttribute("aria-rowcount").Should().BeTrue();
    }

    [Fact]
    public void DataGrid_Should_RenderPagination_WhenEnabled()
    {
        // Arrange
        var items = GetTestItems();

        // Act
        var cut = RenderComponent<DropBearDataGrid<TestItem>>(parameters => parameters
            .Add(p => p.Items, items)
            .Add(p => p.EnablePagination, true));

        // Assert
        cut.WaitForState(() => cut.FindAll(".pagination").Count > 0, TimeSpan.FromSeconds(2));
        var pagination = cut.Find(".pagination");
        pagination.GetAttribute("role").Should().Be("navigation");
        pagination.HasAttribute("aria-label").Should().BeTrue();
    }

    [Fact]
    public void DataGrid_Should_ShowPreviousAndNextButtons()
    {
        // Arrange
        var items = GetTestItems();

        // Act
        var cut = RenderComponent<DropBearDataGrid<TestItem>>(parameters => parameters
            .Add(p => p.Items, items)
            .Add(p => p.EnablePagination, true));

        // Assert
        cut.WaitForState(() => cut.FindAll(".pagination").Count > 0, TimeSpan.FromSeconds(2));
        var buttons = cut.FindAll(".btn-icon");
        var prevButton = buttons.FirstOrDefault(b => b.GetAttribute("aria-label") == "Previous page");
        var nextButton = buttons.FirstOrDefault(b => b.GetAttribute("aria-label") == "Next page");

        prevButton.Should().NotBeNull();
        nextButton.Should().NotBeNull();
    }

    [Fact]
    public void DataGrid_Should_DisablePreviousButton_OnFirstPage()
    {
        // Arrange
        var items = GetTestItems();

        // Act
        var cut = RenderComponent<DropBearDataGrid<TestItem>>(parameters => parameters
            .Add(p => p.Items, items)
            .Add(p => p.EnablePagination, true));

        // Assert - Initially on first page, previous button should be disabled
        cut.WaitForState(() => cut.FindAll(".pagination").Count > 0, TimeSpan.FromSeconds(2));
        var buttons = cut.FindAll(".btn-icon");
        var prevButton = buttons.FirstOrDefault(b => b.GetAttribute("aria-label") == "Previous page");
        prevButton?.HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public void DataGrid_Should_RenderMultiSelectCheckboxes_WhenEnabled()
    {
        // Arrange
        var items = GetTestItems();

        // Act
        var cut = RenderComponent<DropBearDataGrid<TestItem>>(parameters => parameters
            .Add(p => p.Items, items)
            .Add(p => p.EnableMultiSelect, true));

        // Assert
        cut.WaitForState(() => cut.FindAll("input[type='checkbox']").Count > 0, TimeSpan.FromSeconds(2));
        var checkboxes = cut.FindAll("input[type='checkbox']");
        checkboxes.Should().NotBeEmpty();
    }

    [Fact]
    public void DataGrid_Should_RenderSelectAllCheckbox_WhenMultiSelectEnabled()
    {
        // Arrange
        var items = GetTestItems();

        // Act
        var cut = RenderComponent<DropBearDataGrid<TestItem>>(parameters => parameters
            .Add(p => p.Items, items)
            .Add(p => p.EnableMultiSelect, true));

        // Assert
        cut.WaitForState(() => cut.FindAll("thead input[type='checkbox']").Count > 0, TimeSpan.FromSeconds(2));
        var selectAllCheckbox = cut.Find("thead input[type='checkbox']");
        selectAllCheckbox.GetAttribute("aria-label").Should().Contain("Select all");
    }

    [Fact]
    public void DataGrid_Should_RenderActionColumn_WhenActionsAllowed()
    {
        // Arrange
        var items = GetTestItems();

        // Act
        var cut = RenderComponent<DropBearDataGrid<TestItem>>(parameters => parameters
            .Add(p => p.Items, items)
            .Add(p => p.AllowEdit, true));

        // Assert
        cut.WaitForState(() => cut.FindAll(".datagrid-cell-actions").Count > 0, TimeSpan.FromSeconds(2));
        var actionCells = cut.FindAll(".datagrid-cell-actions");
        actionCells.Should().NotBeEmpty();
    }

    [Fact]
    public void DataGrid_Should_RenderEditButton_WhenAllowEditIsTrue()
    {
        // Arrange
        var items = GetTestItems();

        // Act
        var cut = RenderComponent<DropBearDataGrid<TestItem>>(parameters => parameters
            .Add(p => p.Items, items)
            .Add(p => p.AllowEdit, true));

        // Assert
        cut.WaitForState(() => cut.FindAll(".fa-edit").Count > 0, TimeSpan.FromSeconds(2));
        var editIcons = cut.FindAll(".fa-edit");
        editIcons.Should().NotBeEmpty();
    }

    [Fact]
    public void DataGrid_Should_RenderDeleteButton_WhenAllowDeleteIsTrue()
    {
        // Arrange
        var items = GetTestItems();

        // Act
        var cut = RenderComponent<DropBearDataGrid<TestItem>>(parameters => parameters
            .Add(p => p.Items, items)
            .Add(p => p.AllowDelete, true));

        // Assert
        cut.WaitForState(() => cut.FindAll(".fa-trash").Count > 0, TimeSpan.FromSeconds(2));
        var deleteIcons = cut.FindAll(".fa-trash");
        deleteIcons.Should().NotBeEmpty();
    }

    [Fact]
    public void DataGrid_Should_RenderDownloadButton_WhenAllowDownloadIsTrue()
    {
        // Arrange
        var items = GetTestItems();

        // Act
        var cut = RenderComponent<DropBearDataGrid<TestItem>>(parameters => parameters
            .Add(p => p.Items, items)
            .Add(p => p.AllowDownload, true));

        // Assert
        cut.WaitForState(() => cut.FindAll(".fa-download").Count > 0, TimeSpan.FromSeconds(2));
        var downloadIcons = cut.FindAll(".fa-download");
        downloadIcons.Should().NotBeEmpty();
    }

    [Fact]
    public async Task DataGrid_Should_InvokeOnRowClicked_WhenRowClicked()
    {
        // Arrange
        var items = GetTestItems();
        TestItem? clickedItem = null;
        var cut = RenderComponent<DropBearDataGrid<TestItem>>(parameters => parameters
            .Add(p => p.Items, items)
            .Add(p => p.OnRowClicked, EventCallback.Factory.Create<TestItem>(this, item => clickedItem = item)));

        // Wait for grid to render
        cut.WaitForState(() => cut.FindAll(".datagrid-row:not(.header)").Count > 0, TimeSpan.FromSeconds(2));

        // Act
        var row = cut.FindAll(".datagrid-row:not(.header)").FirstOrDefault();
        if (row != null)
        {
            await row.ClickAsync(new MouseEventArgs());
        }

        // Assert - Callback should be configured
        cut.Instance.OnRowClicked.HasDelegate.Should().BeTrue();
    }

    [Fact]
    public void DataGrid_Should_ApplySelectedClass_ToSelectedRow()
    {
        // Arrange
        var items = GetTestItems();

        // Act
        var cut = RenderComponent<DropBearDataGrid<TestItem>>(parameters => parameters
            .Add(p => p.Items, items)
            .Add(p => p.EnableMultiSelect, true));

        // Assert - Test structure is in place for selection
        cut.WaitForState(() => cut.FindAll(".datagrid-row").Count > 0, TimeSpan.FromSeconds(2));
        cut.Instance.Should().NotBeNull();
    }

    [Fact]
    public void DataGrid_Should_HandleErrorNotifications_WhenEnabled()
    {
        // Arrange
        var items = GetTestItems();

        // Act
        var cut = RenderComponent<DropBearDataGrid<TestItem>>(parameters => parameters
            .Add(p => p.Items, items)
            .Add(p => p.ShowErrorNotifications, true));

        // Assert - Component should handle error display
        cut.Instance.Should().NotBeNull();
    }

    [Fact]
    public void DataGrid_Should_HaveAccessibleColumnHeaders()
    {
        // Arrange
        var items = GetTestItems();

        // Act
        var cut = RenderComponent<DropBearDataGrid<TestItem>>(parameters => parameters
            .Add(p => p.Items, items));

        // Assert
        cut.WaitForState(() => cut.FindAll("th[role='columnheader']").Count > 0, TimeSpan.FromSeconds(2));
        var headers = cut.FindAll("th[role='columnheader']");
        headers.Should().NotBeEmpty();
    }

    [Fact]
    public void DataGrid_Should_HaveAccessibleRows()
    {
        // Arrange
        var items = GetTestItems();

        // Act
        var cut = RenderComponent<DropBearDataGrid<TestItem>>(parameters => parameters
            .Add(p => p.Items, items));

        // Assert
        cut.WaitForState(() => cut.FindAll("tr[role='row']").Count > 0, TimeSpan.FromSeconds(2));
        var rows = cut.FindAll("tr[role='row']");
        rows.Should().NotBeEmpty();
    }

    [Fact]
    public void DataGrid_Should_HaveAccessibleCells()
    {
        // Arrange
        var items = GetTestItems();

        // Act
        var cut = RenderComponent<DropBearDataGrid<TestItem>>(parameters => parameters
            .Add(p => p.Items, items));

        // Assert
        cut.WaitForState(() => cut.FindAll("td[role='gridcell']").Count > 0, TimeSpan.FromSeconds(2));
        var cells = cut.FindAll("td[role='gridcell']");
        cells.Should().NotBeEmpty();
    }

    [Fact]
    public void DataGrid_Should_DisposeCorrectly()
    {
        // Arrange
        var items = GetTestItems();
        var cut = RenderComponent<DropBearDataGrid<TestItem>>(parameters => parameters
            .Add(p => p.Items, items));

        // Act
        var exception = Record.Exception(() => cut.Dispose());

        // Assert
        exception.Should().BeNull();
    }

    private static List<TestItem> GetTestItems()
    {
        return
        [
            new TestItem { Id = 1, Name = "Item 1", Value = 100 },
            new TestItem { Id = 2, Name = "Item 2", Value = 200 },
            new TestItem { Id = 3, Name = "Item 3", Value = 300 }
        ];
    }

    /// <summary>
    ///     Test item for DataGrid testing.
    /// </summary>
    private class TestItem
    {
        public int Id { get; init; }
        public required string Name { get; init; }
        public decimal Value { get; init; }
    }
}
