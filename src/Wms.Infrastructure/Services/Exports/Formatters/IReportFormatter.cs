using Wms.Domain.Enums;

namespace Wms.Infrastructure.Services.Exports.Formatters;

public interface IReportFormatter
{
    ExportFormat SupportedFormat { get; }
    string ContentType { get; }
    string FileExtension { get; }
    byte[] Format(string title, IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<object?>> rows, IReadOnlyDictionary<string, string>? metadata = null);
}
