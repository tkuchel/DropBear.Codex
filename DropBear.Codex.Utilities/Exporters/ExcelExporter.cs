#region

using System.Globalization;
using System.Reflection;
using ClosedXML.Excel;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Utilities.Errors;
using Serilog;

#endregion

namespace DropBear.Codex.Utilities.Exporters;

/// <summary>
///     Provides functionality to export a list of objects to an Excel spreadsheet.
/// </summary>
/// <typeparam name="T">The type of objects to export.</typeparam>
public sealed class ExcelExporter<T> where T : class
{
    private static readonly ILogger Logger = LoggerFactory.Logger.ForContext<ExcelExporter<T>>();
    private static readonly PropertyInfo[] _properties;
    private static readonly Dictionary<Type, Action<IXLCell, object>> _typeHandlers;

    static ExcelExporter()
    {
        _properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

        _typeHandlers = new Dictionary<Type, Action<IXLCell, object>>
        {
            [typeof(int)] = (cell, value) => cell.SetValue(Convert.ToInt32(value, CultureInfo.InvariantCulture)),
            [typeof(double)] = (cell, value) => cell.SetValue(Convert.ToDouble(value, CultureInfo.InvariantCulture)),
            [typeof(decimal)] = (cell, value) => cell.SetValue(Convert.ToDecimal(value, CultureInfo.InvariantCulture)),
            [typeof(bool)] = (cell, value) => cell.SetValue(Convert.ToBoolean(value)),
            [typeof(DateTime)] = (cell, value) =>
            {
                cell.SetValue(Convert.ToDateTime(value, CultureInfo.InvariantCulture));
                cell.Style.DateFormat.Format = "yyyy-MM-dd hh:mm:ss";
            }
        };
    }

    /// <summary>
    ///     Exports the provided list of objects to an Excel file.
    /// </summary>
    /// <param name="data">The list of objects to export.</param>
    /// <param name="filePath">The file path where the Excel file will be saved.</param>
    /// <param name="options">Optional export configuration.</param>
    /// <returns>A result containing the file path or an error.</returns>
    public Result<string, ExportError> ExportToExcel(List<T> data, string filePath, ExcelExportOptions? options = null)
    {
        options ??= new ExcelExportOptions();

        if (data == null)
        {
            return Result<string, ExportError>.Failure(new ExportError("Data cannot be null."));
        }

        if (string.IsNullOrWhiteSpace(filePath))
        {
            return Result<string, ExportError>.Failure(new ExportError("File path cannot be null or empty."));
        }

        try
        {
            using var workbook = CreateWorkbook(data, options);
            workbook.SaveAs(filePath);

            Logger.Information("Data exported to Excel file at '{FilePath}' successfully.", filePath);
            return Result<string, ExportError>.Success(filePath);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "An error occurred while exporting data to Excel file '{FilePath}'.", filePath);
            return Result<string, ExportError>.Failure(new ExportError($"Export failed: {ex.Message}"), ex);
        }
    }

    /// <summary>
    ///     Exports the provided list of objects to an Excel file asynchronously.
    /// </summary>
    /// <param name="data">The list of objects to export.</param>
    /// <param name="filePath">The file path where the Excel file will be saved.</param>
    /// <param name="options">Optional export configuration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result containing the file path or an error.</returns>
    public async Task<Result<string, ExportError>> ExportToExcelAsync(
        List<T> data,
        string filePath,
        ExcelExportOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new ExcelExportOptions();

        if (data == null)
        {
            return Result<string, ExportError>.Failure(new ExportError("Data cannot be null."));
        }

        if (string.IsNullOrWhiteSpace(filePath))
        {
            return Result<string, ExportError>.Failure(new ExportError("File path cannot be null or empty."));
        }

        try
        {
            var result = await Task.Run(() =>
            {
                using var workbook = CreateWorkbook(data, options);
                workbook.SaveAs(filePath);
                return filePath;
            }, cancellationToken);

            Logger.Information("Data exported to Excel file at '{FilePath}' successfully.", filePath);
            return Result<string, ExportError>.Success(result);
        }
        catch (OperationCanceledException)
        {
            Logger.Warning("Excel export operation was cancelled for file '{FilePath}'.", filePath);
            return Result<string, ExportError>.Cancelled(string.Empty,new ExportError("Export operation was cancelled."));
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "An error occurred while exporting data to Excel file '{FilePath}'.", filePath);
            return Result<string, ExportError>.Failure(new ExportError($"Export failed: {ex.Message}"), ex);
        }
    }

    /// <summary>
    ///     Exports the provided list of objects to an Excel file and returns a MemoryStream.
    /// </summary>
    /// <param name="data">The list of objects to export.</param>
    /// <param name="options">Optional export configuration.</param>
    /// <returns>A result containing a memory stream with the Excel file or an error.</returns>
    public Result<MemoryStream, ExportError> ExportToExcelStream(List<T> data, ExcelExportOptions? options = null)
    {
        options ??= new ExcelExportOptions();

        if (data == null)
        {
            return Result<MemoryStream, ExportError>.Failure(new ExportError("Data cannot be null."));
        }

        try
        {
            using var workbook = CreateWorkbook(data, options);
            var memoryStream = new MemoryStream();
            workbook.SaveAs(memoryStream);
            memoryStream.Position = 0; // Reset the stream position to the beginning

            Logger.Information("Data exported to Excel stream successfully.");
            return Result<MemoryStream, ExportError>.Success(memoryStream);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "An error occurred while exporting data to Excel stream.");
            return Result<MemoryStream, ExportError>.Failure(new ExportError($"Stream export failed: {ex.Message}"),
                ex);
        }
    }

    /// <summary>
    ///     Exports the provided list of objects to an Excel stream asynchronously.
    /// </summary>
    /// <param name="data">The list of objects to export.</param>
    /// <param name="options">Optional export configuration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result containing a memory stream with the Excel file or an error.</returns>
    public async Task<Result<MemoryStream, ExportError>> ExportToExcelStreamAsync(
        List<T> data,
        ExcelExportOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new ExcelExportOptions();

        if (data == null)
        {
            return Result<MemoryStream, ExportError>.Failure(new ExportError("Data cannot be null."));
        }

        try
        {
            return await Task.Run(() =>
            {
                using var workbook = CreateWorkbook(data, options);
                var memoryStream = new MemoryStream();
                workbook.SaveAs(memoryStream);
                memoryStream.Position = 0;
                return Result<MemoryStream, ExportError>.Success(memoryStream);
            }, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            Logger.Warning("Excel stream export operation was cancelled.");
            return Result<MemoryStream, ExportError>.Cancelled(new ExportError("Export operation was cancelled."));
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "An error occurred while exporting data to Excel stream.");
            return Result<MemoryStream, ExportError>.Failure(new ExportError($"Stream export failed: {ex.Message}"),
                ex);
        }
    }

    /// <summary>
    ///     Creates an XLWorkbook from the provided data.
    /// </summary>
    /// <param name="data">The list of objects to include in the workbook.</param>
    /// <param name="options">Export options.</param>
    /// <returns>An XLWorkbook containing the data.</returns>
    private XLWorkbook CreateWorkbook(List<T> data, ExcelExportOptions options)
    {
        try
        {
            Logger.Information("Creating Excel workbook for data of type '{TypeName}'.", typeof(T).Name);

            var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add(options.SheetName);

            // Process headers if needed
            if (options.IncludeHeaders)
            {
                for (var i = 0; i < _properties.Length; i++)
                {
                    var propName = _properties[i].Name;
                    var headerName = options.HeaderMappings?.TryGetValue(propName, out var mapping) == true
                        ? mapping
                        : propName;

                    worksheet.Cell(1, i + 1).Value = headerName;
                }
            }

            // Determine actual row count to process
            var rowCount = options.MaximumRows.HasValue
                ? Math.Min(data.Count, options.MaximumRows.Value)
                : data.Count;

            // Process data in chunks for better memory management
            const int chunkSize = 1000;

            for (var rowOffset = 0; rowOffset < rowCount; rowOffset += chunkSize)
            {
                var chunkEnd = Math.Min(rowOffset + chunkSize, rowCount);
                var chunk = data.GetRange(rowOffset, chunkEnd - rowOffset);

                ProcessDataChunk(worksheet, chunk, rowOffset + (options.IncludeHeaders ? 2 : 1));
            }

            // Adjust column widths if needed
            if (options.AutoAdjustColumns)
            {
                worksheet.Columns().AdjustToContents();
            }

            Logger.Information("Excel workbook created successfully.");
            return workbook;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "An error occurred while creating the Excel workbook.");
            throw;
        }
    }

    /// <summary>
    ///     Processes a chunk of data to add to the worksheet.
    /// </summary>
    private void ProcessDataChunk(IXLWorksheet worksheet, List<T> chunk, int startRow)
    {
        for (var rowIndex = 0; rowIndex < chunk.Count; rowIndex++)
        {
            var item = chunk[rowIndex];
            var actualRow = startRow + rowIndex;

            for (var colIndex = 0; colIndex < _properties.Length; colIndex++)
            {
                var property = _properties[colIndex];
                var cell = worksheet.Cell(actualRow, colIndex + 1);

                try
                {
                    var value = property.GetValue(item);

                    if (value == null)
                    {
                        cell.SetValue(string.Empty);
                        continue;
                    }

                    SetCellValue(cell, value, property.PropertyType);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex,
                        "An error occurred while processing property '{PropertyName}' at row {RowIndex}, column {ColIndex}.",
                        property.Name, actualRow, colIndex + 1);
                    cell.SetValue(string.Empty);
                }
            }
        }
    }

    /// <summary>
    ///     Sets the value of a cell based on the property's type.
    /// </summary>
    private static void SetCellValue(IXLCell cell, object value, Type propertyType)
    {
        try
        {
            var underlyingType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;

            if (_typeHandlers.TryGetValue(underlyingType, out var handler))
            {
                handler(cell, value);
            }
            else
            {
                // For other types, convert to string
                cell.SetValue(value.ToString() ?? string.Empty);
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "An error occurred while setting cell value for property type '{PropertyType}'.",
                propertyType.Name);
            cell.SetValue(string.Empty);
        }
    }
}
