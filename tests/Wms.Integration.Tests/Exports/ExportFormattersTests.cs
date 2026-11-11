using ClosedXML.Excel;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Features.Audit;
using Wms.Application.Features.Exports;
using Wms.Domain.Enums;
using Wms.Infrastructure.Persistence;
using Wms.Infrastructure.Services.Audit;
using Wms.Infrastructure.Services.Exports;
using Wms.Infrastructure.Services.Exports.FileStorage;
using Wms.Infrastructure.Services.Exports.Formatters;
using Wms.Infrastructure.Services.Notifications;
using Xunit;

namespace Wms.Integration.Tests.Exports;

public class ExportFormattersTests
{
    private readonly List<string> _headers = new() { "SKU", "Nama Barang", "Stok", "Harga Satuan" };
    private readonly List<IReadOnlyList<object?>> _rows = new()
    {
        new object?[] { "SKU-001", "Kopi Robusta 1kg", 50, 75000m },
        new object?[] { "SKU-002", "Gula Pasir 1kg", 120, 14000m }
    };
    private readonly Dictionary<string, string> _metadata = new()
    {
        { "Gudang", "Gudang Pusat" },
        { "Tanggal", "2026-11-11" }
    };

    [Fact]
    public void CsvReportFormatter_ShouldGenerateValidCsvBytes()
    {
        var formatter = new CsvReportFormatter();
        var bytes = formatter.Format("Laporan Stok", _headers, _rows, _metadata);

        bytes.Should().NotBeNullOrEmpty();
        var csvString = System.Text.Encoding.UTF8.GetString(bytes);
        csvString.Should().Contain("SKU,Nama Barang,Stok,Harga Satuan");
        csvString.Should().Contain("SKU-001,Kopi Robusta 1kg,50,75000");
    }

    [Fact]
    public void ExcelReportFormatter_ShouldGenerateValidWorkbook()
    {
        var formatter = new ExcelReportFormatter();
        var bytes = formatter.Format("Laporan Stok", _headers, _rows, _metadata);

        bytes.Should().NotBeNullOrEmpty();
        using var stream = new MemoryStream(bytes);
        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheet(1);

        worksheet.Should().NotBeNull();
        worksheet.Cell(1, 1).GetString().Should().Be("Laporan Stok");
    }

    [Fact]
    public void PdfReportFormatter_ShouldGenerateValidPdfDocument()
    {
        var formatter = new PdfReportFormatter();
        var bytes = formatter.Format("Laporan Stok", _headers, _rows, _metadata);

        bytes.Should().NotBeNullOrEmpty();
        // PDF magic header
        var header = System.Text.Encoding.ASCII.GetString(bytes.Take(4).ToArray());
        header.Should().Be("%PDF");
    }

    [Fact]
    public async Task ExportService_DirectExport_ShouldFormatAndLogAudit()
    {
        var options = new DbContextOptionsBuilder<WmsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        using var context = new WmsDbContext(options);

        var fileStorage = new LocalExportFileStorage();
        var formatters = new IReportFormatter[]
        {
            new CsvReportFormatter(),
            new ExcelReportFormatter(),
            new PdfReportFormatter()
        };
        var auditService = new AppAuditService(context);
        var notifService = new NotificationService(context);

        var exportService = new ExportService(context, fileStorage, formatters, auditService, notifService);

        var request = new DirectExportRequest(
            ReportType: "StockReport",
            Format: ExportFormat.Csv,
            Title: "Laporan Stok",
            Headers: _headers,
            Rows: _rows,
            Metadata: _metadata,
            RequestedByUserId: Guid.NewGuid().ToString(),
            RequestedByUserName: "Admin"
        );

        var result = await exportService.ExportDirectAsync(request);

        result.Succeeded.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.RowCount.Should().Be(2);
        result.Data.FileName.Should().StartWith("StockReport_").And.EndWith(".csv");

        // Verify Audit Log was created
        var auditLogs = await context.AuditLogs.ToListAsync();
        auditLogs.Should().ContainSingle(a => a.Action == "Export" && a.EntityType == "Report");
    }
}
