using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Wms.Domain.Enums;

namespace Wms.Infrastructure.Services.Exports.Formatters;

public class PdfReportFormatter : IReportFormatter
{
    public ExportFormat SupportedFormat => ExportFormat.Pdf;
    public string ContentType => "application/pdf";
    public string FileExtension => "pdf";

    static PdfReportFormatter()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] Format(string title, IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<object?>> rows, IReadOnlyDictionary<string, string>? metadata = null)
    {
        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(1.5f, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Arial"));

                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(titleCol =>
                        {
                            titleCol.Item().Text("WMS NUSANTARA").Bold().FontSize(14).FontColor("#1B365D");
                            titleCol.Item().Text(title).SemiBold().FontSize(12);
                            if (metadata != null)
                            {
                                foreach (var (k, v) in metadata)
                                {
                                    titleCol.Item().Text($"{k}: {v}").FontSize(8).FontColor(Colors.Grey.Medium);
                                }
                            }
                        });
                        row.ConstantItem(180).AlignRight().Text($"Dicetak: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC").FontSize(8).FontColor(Colors.Grey.Medium);
                    });
                    col.Item().PaddingVertical(5).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                });

                page.Content().PaddingTop(5).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        for (int i = 0; i < headers.Count; i++)
                        {
                            columns.RelativeColumn();
                        }
                    });

                    table.Header(header =>
                    {
                        for (int i = 0; i < headers.Count; i++)
                        {
                            header.Cell().Background("#1B365D").Padding(4).Text(headers[i]).Bold().FontColor(Colors.White).FontSize(8);
                        }
                    });

                    for (int r = 0; r < rows.Count; r++)
                    {
                        var rowData = rows[r];
                        var bg = r % 2 == 0 ? Colors.White : Colors.Grey.Lighten4;
                        for (int c = 0; c < rowData.Count; c++)
                        {
                            var text = rowData[c]?.ToString() ?? "-";
                            table.Cell().Background(bg).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(text).FontSize(8);
                        }
                    }
                });

                page.Footer().AlignRight().Text(text =>
                {
                    text.Span("Halaman ");
                    text.CurrentPageNumber();
                    text.Span(" dari ");
                    text.TotalPages();
                });
            });
        });

        return doc.GeneratePdf();
    }
}
