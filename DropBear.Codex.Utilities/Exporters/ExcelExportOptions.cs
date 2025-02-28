namespace DropBear.Codex.Utilities.Exporters;

/// <summary>
///     Provides customization options for Excel export operations.
///     Controls various aspects of how data is exported to Excel files.
/// </summary>
public sealed class ExcelExportOptions
{
    /// <summary>
    ///     Gets or sets the name of the worksheet in the exported Excel file.
    ///     Defaults to "Sheet1".
    /// </summary>
    public string SheetName { get; set; } = "Sheet1";

    /// <summary>
    ///     Gets or sets a value indicating whether column headers should be included in the export.
    ///     Headers are derived from property names or custom mappings.
    ///     Defaults to true.
    /// </summary>
    public bool IncludeHeaders { get; set; } = true;

    /// <summary>
    ///     Gets or sets a value indicating whether column widths should be automatically adjusted to fit content.
    ///     This may increase export time for large datasets.
    ///     Defaults to true.
    /// </summary>
    public bool AutoAdjustColumns { get; set; } = true;

    /// <summary>
    ///     Gets or sets optional mappings from property names to custom header names.
    ///     When provided, these custom names will be used instead of the property names for column headers.
    ///     The dictionary keys should match the property names of the exported type.
    /// </summary>
    public Dictionary<string, string>? HeaderMappings { get; set; }

    /// <summary>
    ///     Gets or sets an optional limit on the maximum number of rows to export.
    ///     When specified, only up to this many rows will be included in the output.
    ///     Useful for creating previews or limiting export size.
    ///     When null (the default), all rows will be exported.
    /// </summary>
    public int? MaximumRows { get; set; }
}
