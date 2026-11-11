using ClosedXML.Excel;
using Wms.Domain.Enums;

namespace Wms.Infrastructure.Services.Exports.Formatters;

public class ExcelReportFormatter : IReportFormatter
{
    public ExportFormat SupportedFormat => ExportFormat.Xlsx;
    public string ContentType => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    public string FileExtension => "xlsx";

    public byte[] Format(string title, IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<object?>> rows, IReadOnlyDictionary<string, string>? metadata = null)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Laporan");

        int currentRow = 1;

        // Title
        worksheet.Cell(currentRow, 1).Value = title;
        worksheet.Cell(currentRow, 1).Style.Font.Bold = true;
        worksheet.Cell(currentRow, 1).Style.Font.FontSize = 14;
        currentRow++;

        // Metadata
        if (metadata != null && metadata.Count > 0)
        {
            foreach (var kvp in metadata)
            {
                worksheet.Cell(currentRow, 1).Value = $"{kvp.Key}: {kvp.Value}";
                worksheet.Cell(currentRow, 1).Style.Font.Italic = true;
                currentRow++;
            }
        }
        currentRow++; // blank line

        // Headers
        int headerRow = currentRow;
        for (int i = 0; i < headers.Count; i++)
        {
            var cell = worksheet.Cell(headerRow, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1B365D");
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }
        currentRow++;

        // Data Rows
        for (int r = 0; r < rows.Count; r++)
        {
            var rowData = rows[r];
            for (int c = 0; c < rowData.Count; c++)
            {
                var val = rowData[c];
                var cell = worksheet.Cell(currentRow, c + 1);
                if (val is null)
                {
                    cell.Value = string.Empty;
                }
                else if (val is int intVal)
                {
                    cell.Value = intVal;
                }
                else if (val is long longVal)
                {
                    cell.Value = longVal;
                }
                else if (val is decimal decVal)
                {
                    cell.Value = decVal;
                }
                else if (val is double dblVal)
                {
                    cell.Value = dblVal;
                }
                else if (val is DateTime dtVal)
                {
                    cell.Value = dtVal.ToString("yyyy-MM-dd HH:mm");
                }
                else if (val is bool bVal)
                {
                    cell.Value = bVal ? "Ya" : "Tidak";
                }
                else
                {
                    cell.Value = val.ToString() ?? string.Empty;
                }
            }
            currentRow++;
        }

        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
