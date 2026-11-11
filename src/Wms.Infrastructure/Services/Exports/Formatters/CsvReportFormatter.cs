using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using Wms.Domain.Enums;

namespace Wms.Infrastructure.Services.Exports.Formatters;

public class CsvReportFormatter : IReportFormatter
{
    public ExportFormat SupportedFormat => ExportFormat.Csv;
    public string ContentType => "text/csv; charset=utf-8";
    public string FileExtension => "csv";

    public byte[] Format(string title, IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<object?>> rows, IReadOnlyDictionary<string, string>? metadata = null)
    {
        using var memoryStream = new MemoryStream();
        using (var writer = new StreamWriter(memoryStream, new UTF8Encoding(true)))
        using (var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture)))
        {
            // Write headers
            foreach (var header in headers)
            {
                csv.WriteField(header);
            }
            csv.NextRecord();

            // Write rows
            foreach (var row in rows)
            {
                foreach (var cell in row)
                {
                    csv.WriteField(cell?.ToString() ?? string.Empty);
                }
                csv.NextRecord();
            }
        }

        return memoryStream.ToArray();
    }
}
