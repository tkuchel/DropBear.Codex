#region

using System.Globalization;
using System.Reflection;
using ClosedXML.Excel;
using DropBear.Codex.Core.Logging;
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
    private readonly PropertyInfo[] _properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

    /// <summary>
    ///     Exports the provided list of objects to an Excel file.
    /// </summary>
    /// <param name="data">The list of objects to export.</param>
    /// <param name="filePath">The file path where the Excel file will be saved.</param>
    public void ExportToExcel(List<T> data, string filePath)
    {
        if (data == null)
        {
            throw new ArgumentNullException(nameof(data), "Data cannot be null.");
        }

        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));
        }

        try
        {
            using var workbook = CreateWorkbook(data);
            workbook.SaveAs(filePath);

            Logger.Information("Data exported to Excel file at '{FilePath}' successfully.", filePath);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "An error occurred while exporting data to Excel file '{FilePath}'.", filePath);
            throw;
        }
    }

    /// <summary>
    ///     Exports the provided list of objects to an Excel file and returns a MemoryStream.
    /// </summary>
    /// <param name="data">The list of objects to export.</param>
    /// <returns>A MemoryStream containing the Excel file.</returns>
    public MemoryStream ExportToExcelStream(List<T> data)
    {
        if (data == null)
        {
            throw new ArgumentNullException(nameof(data), "Data cannot be null.");
        }

        try
        {
            using var workbook = CreateWorkbook(data);
            var memoryStream = new MemoryStream();
            workbook.SaveAs(memoryStream);
            memoryStream.Position = 0; // Reset the stream position to the beginning

            Logger.Information("Data exported to Excel stream successfully.");

            return memoryStream;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "An error occurred while exporting data to Excel stream.");
            throw;
        }
    }

    /// <summary>
    ///     Creates an XLWorkbook from the provided data.
    /// </summary>
    /// <param name="data">The list of objects to include in the workbook.</param>
    /// <returns>An XLWorkbook containing the data.</returns>
    private XLWorkbook CreateWorkbook(List<T> data)
    {
        try
        {
            Logger.Information("Creating Excel workbook for data of type '{TypeName}'.", typeof(T).Name);

            var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Sheet1");

            var properties = _properties;

            // Add header row
            for (var i = 0; i < properties.Length; i++)
            {
                worksheet.Cell(1, i + 1).Value = properties[i].Name;
            }

            // Add data rows
            for (var rowIndex = 0; rowIndex < data.Count; rowIndex++)
            {
                var item = data[rowIndex];
                for (var colIndex = 0; colIndex < properties.Length; colIndex++)
                {
                    var property = properties[colIndex];
                    var cell = worksheet.Cell(rowIndex + 2, colIndex + 1);

                    try
                    {
                        var value = property.GetValue(item);

                        if (value == null)
                        {
                            // Set the cell to empty
                            cell.SetValue(string.Empty);
                        }
                        else
                        {
                            SetCellValue(cell, value, property.PropertyType);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex,
                            "An error occurred while processing property '{PropertyName}' at row {RowIndex}, column {ColIndex}.",
                            property.Name, rowIndex + 2, colIndex + 1);
                        cell.SetValue(string.Empty);
                    }
                }
            }

            // Adjust column widths
            worksheet.Columns().AdjustToContents();

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
    ///     Sets the value of a cell based on the property's type.
    /// </summary>
    /// <param name="cell">The cell to set the value for.</param>
    /// <param name="value">The value to set.</param>
    /// <param name="propertyType">The type of the property.</param>
    private void SetCellValue(IXLCell cell, object value, Type propertyType)
    {
        try
        {
            var underlyingType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;

            if (underlyingType == typeof(int))
            {
                cell.SetValue(Convert.ToInt32(value, CultureInfo.InvariantCulture));
            }
            else if (underlyingType == typeof(double))
            {
                cell.SetValue(Convert.ToDouble(value, CultureInfo.InvariantCulture));
            }
            else if (underlyingType == typeof(decimal))
            {
                cell.SetValue(Convert.ToDecimal(value, CultureInfo.InvariantCulture));
            }
            else if (underlyingType == typeof(bool))
            {
                cell.SetValue(Convert.ToBoolean(value));
            }
            else if (underlyingType == typeof(DateTime))
            {
                cell.SetValue(Convert.ToDateTime(value, CultureInfo.InvariantCulture));
                cell.Style.DateFormat.Format = "yyyy-mm-dd hh:mm:ss";
            }
            else if (underlyingType == typeof(TimeSpan))
            {
                cell.SetValue(value.ToString());
            }
            else if (underlyingType == typeof(string))
            {
                cell.SetValue(value.ToString());
            }
            else if (underlyingType.IsEnum)
            {
                cell.SetValue(value.ToString());
            }
            else
            {
                // For other types, convert to string
                cell.SetValue(value.ToString());
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
