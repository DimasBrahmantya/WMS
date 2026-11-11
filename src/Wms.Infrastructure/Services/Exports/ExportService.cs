using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Interfaces;
using Wms.Application.Common.Models;
using Wms.Application.Features.Audit;
using Wms.Application.Features.Exports;
using Wms.Application.Features.Notifications;
using Wms.Domain.Entities.Exports;
using Wms.Domain.Enums;
using Wms.Infrastructure.Services.Exports.FileStorage;
using Wms.Infrastructure.Services.Exports.Formatters;

namespace Wms.Infrastructure.Services.Exports;

public class ExportService : IExportService
{
    private readonly IWmsDbContext _context;
    private readonly IExportFileStorage _fileStorage;
    private readonly IEnumerable<IReportFormatter> _formatters;
    private readonly IAppAuditService _auditService;
    private readonly INotificationService _notificationService;

    public ExportService(
        IWmsDbContext context,
        IExportFileStorage fileStorage,
        IEnumerable<IReportFormatter> formatters,
        IAppAuditService auditService,
        INotificationService notificationService)
    {
        _context = context;
        _fileStorage = fileStorage;
        _formatters = formatters;
        _auditService = auditService;
        _notificationService = notificationService;
    }

    public async Task<Result<DirectExportResult>> ExportDirectAsync(
        DirectExportRequest request,
        CancellationToken cancellationToken = default)
    {
        var formatter = _formatters.FirstOrDefault(f => f.SupportedFormat == request.Format);
        if (formatter == null)
        {
            return Result<DirectExportResult>.Failure($"Format ekspor {request.Format} tidak didukung.");
        }

        var bytes = formatter.Format(request.Title, request.Headers, request.Rows, request.Metadata);
        var fileName = $"{request.ReportType}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.{formatter.FileExtension}";

        Guid? userGuid = Guid.TryParse(request.RequestedByUserId, out var parsedGuid) ? parsedGuid : null;
        await _auditService.LogAsync(
            "Report",
            request.ReportType,
            "Export",
            userGuid,
            null,
            $"{{\"format\":\"{request.Format}\",\"rows\":{request.Rows.Count},\"fileName\":\"{fileName}\"}}",
            $"Ekspor langsung laporan {request.Title}",
            cancellationToken);

        var result = new DirectExportResult(bytes, formatter.ContentType, fileName, request.Rows.Count);
        return Result<DirectExportResult>.Success(result);
    }

    public async Task<Result<Guid>> QueueExportJobAsync(
        QueueExportJobRequest request,
        CancellationToken cancellationToken = default)
    {
        var job = new ExportJob
        {
            RequestedByUserId = request.RequestedByUserId,
            RequestedByUserName = request.RequestedByUserName,
            ReportType = request.ReportType,
            Format = request.Format,
            FilterPayloadJson = request.FilterPayloadJson,
            Status = ExportJobStatus.Pending,
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddHours(24)
        };

        _context.ExportJobs.Add(job);
        await _context.SaveChangesAsync(cancellationToken);

        Guid? userGuid = Guid.TryParse(request.RequestedByUserId, out var parsedGuid) ? parsedGuid : null;
        await _auditService.LogAsync(
            "ExportJob",
            job.Id.ToString(),
            "QueueExport",
            userGuid,
            null,
            $"{{\"reportType\":\"{request.ReportType}\",\"format\":\"{request.Format}\"}}",
            $"Antrean ekspor latar belakang untuk {request.ReportType}",
            cancellationToken);

        return Result<Guid>.Success(job.Id);
    }

    public async Task<ExportJobDto?> GetExportJobAsync(
        Guid jobId,
        CancellationToken cancellationToken = default)
    {
        var job = await _context.ExportJobs.FindAsync(new object[] { jobId }, cancellationToken);
        if (job == null) return null;

        return MapToDto(job);
    }

    public async Task<Result<(byte[] FileBytes, string ContentType, string FileName)>> GetExportFileAsync(
        Guid jobId,
        string userId,
        bool isSuperAdmin,
        CancellationToken cancellationToken = default)
    {
        var job = await _context.ExportJobs.FindAsync(new object[] { jobId }, cancellationToken);
        if (job == null)
        {
            return Result<(byte[], string, string)>.Failure("Tiket ekspor tidak ditemukan.");
        }

        if (!isSuperAdmin && job.RequestedByUserId != userId)
        {
            return Result<(byte[], string, string)>.Failure("Anda tidak memiliki hak akses untuk mengunduh berkas ini.");
        }

        if (job.Status != ExportJobStatus.Completed || string.IsNullOrEmpty(job.FilePath))
        {
            return Result<(byte[], string, string)>.Failure("Berkas ekspor belum selesai diproses atau gagal.");
        }

        var bytes = await _fileStorage.ReadFileAsync(job.FilePath, cancellationToken);
        if (bytes == null || bytes.Length == 0)
        {
            return Result<(byte[], string, string)>.Failure("Berkas fisik ekspor tidak ditemukan atau telah kedaluwarsa.");
        }

        var formatter = _formatters.FirstOrDefault(f => f.SupportedFormat == job.Format);
        var contentType = formatter?.ContentType ?? "application/octet-stream";
        var fileName = job.FileName ?? $"{job.ReportType}_{job.Id}.dat";

        Guid? userGuid = Guid.TryParse(userId, out var parsedGuid) ? parsedGuid : null;
        await _auditService.LogAsync(
            "ExportJob",
            job.Id.ToString(),
            "Download",
            userGuid,
            null,
            $"{{\"fileName\":\"{fileName}\",\"sizeBytes\":{bytes.Length}}}",
            $"Pengunduhan berkas ekspor {job.ReportType}",
            cancellationToken);

        return Result<(byte[], string, string)>.Success((bytes, contentType, fileName));
    }

    public async Task<Result> ProcessExportJobAsync(
        Guid jobId,
        CancellationToken cancellationToken = default)
    {
        var job = await _context.ExportJobs.FindAsync(new object[] { jobId }, cancellationToken);
        if (job == null || job.Status != ExportJobStatus.Pending)
        {
            return Result.Failure("Job tidak valid atau sedang diproses.");
        }

        job.MarkProcessing();
        await _context.SaveChangesAsync(cancellationToken);

        try
        {
            var formatter = _formatters.FirstOrDefault(f => f.SupportedFormat == job.Format);
            if (formatter == null)
            {
                throw new InvalidOperationException($"Format {job.Format} tidak didukung.");
            }

            var (title, headers, rows, metadata) = await BuildReportDataAsync(job, cancellationToken);

            var bytes = formatter.Format(title, headers, rows, metadata);
            var safeFileName = $"{job.ReportType}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.{formatter.FileExtension}";
            var storagePath = await _fileStorage.SaveFileAsync($"{job.Id}_{safeFileName}", bytes, cancellationToken);

            job.MarkCompleted(storagePath, safeFileName, rows.Count, bytes.LongLength, DateTime.UtcNow);
            await _context.SaveChangesAsync(cancellationToken);

            // Kirim notifikasi in-app ke pengguna peminta
            await _notificationService.PublishNotificationAsync(new PublishNotificationRequest(
                UserId: job.RequestedByUserId,
                TargetRole: null,
                Category: NotificationCategory.SystemExport,
                Title: "Laporan Siap Diunduh",
                Message: $"Laporan {title} ({job.Format}) siap diunduh ({rows.Count} baris).",
                LinkUrl: $"/api/exports/download/{job.Id}",
                DedupKey: $"ExportJob:{job.Id}"
            ), cancellationToken);

            return Result.Success();
        }
        catch (Exception ex)
        {
            job.MarkFailed(ex.Message, DateTime.UtcNow);
            await _context.SaveChangesAsync(cancellationToken);

            await _notificationService.PublishNotificationAsync(new PublishNotificationRequest(
                UserId: job.RequestedByUserId,
                TargetRole: null,
                Category: NotificationCategory.SystemExport,
                Title: "Ekspor Gagal",
                Message: $"Proses ekspor {job.ReportType} mengalami kendala: {ex.Message}",
                LinkUrl: null,
                DedupKey: $"ExportJobFailed:{job.Id}"
            ), cancellationToken);

            return Result.Failure(ex.Message);
        }
    }

    public async Task<Result> CleanupExpiredExportsAsync(CancellationToken cancellationToken = default)
    {
        var expiredJobs = await _context.ExportJobs
            .Where(j => j.ExpiresAtUtc <= DateTime.UtcNow && j.Status == ExportJobStatus.Completed)
            .ToListAsync(cancellationToken);

        foreach (var job in expiredJobs)
        {
            if (!string.IsNullOrEmpty(job.FilePath))
            {
                await _fileStorage.DeleteFileAsync(job.FilePath, cancellationToken);
            }
            job.MarkExpired();
        }

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private async Task<(string Title, IReadOnlyList<string> Headers, IReadOnlyList<IReadOnlyList<object?>> Rows, IReadOnlyDictionary<string, string>? Metadata)> BuildReportDataAsync(
        ExportJob job,
        CancellationToken cancellationToken)
    {
        var metadata = new Dictionary<string, string>
        {
            { "Diminta Oleh", job.RequestedByUserName },
            { "Waktu Proses", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC") }
        };

        if (job.ReportType.Equals("StockReport", StringComparison.OrdinalIgnoreCase))
        {
            var balances = await _context.InventoryBalances
                .Include(b => b.Product)
                .Include(b => b.Warehouse)
                .AsNoTracking()
                .OrderBy(b => b.Warehouse.Name)
                .ThenBy(b => b.Product.Sku)
                .ToListAsync(cancellationToken);

            var headers = new List<string> { "Gudang", "SKU", "Nama Barang", "Kategori", "Jumlah Stok", "Satuan" };
            var rows = balances.Select(b => (IReadOnlyList<object?>)new object?[]
            {
                b.Warehouse.Name,
                b.Product.Sku,
                b.Product.Name,
                b.Product.CategoryId.ToString(),
                b.OnHand,
                b.Product.UnitId.ToString()
            }).ToList();

            return ("Laporan Stok Saat Ini", headers, rows, metadata);
        }
        else if (job.ReportType.Equals("ValuationReport", StringComparison.OrdinalIgnoreCase))
        {
            var valuations = await _context.InventoryValuations
                .Include(v => v.Product)
                .Include(v => v.Warehouse)
                .AsNoTracking()
                .OrderBy(v => v.Warehouse.Name)
                .ThenBy(v => v.Product.Sku)
                .ToListAsync(cancellationToken);

            var headers = new List<string> { "Gudang", "SKU", "Nama Barang", "Kuantitas", "Biaya Rata-Rata", "Total Nilai" };
            var rows = valuations.Select(v => (IReadOnlyList<object?>)new object?[]
            {
                v.Warehouse.Name,
                v.Product.Sku,
                v.Product.Name,
                v.OnHandQty,
                v.AvgCost,
                v.OnHandValue
            }).ToList();

            return ("Laporan Nilai Persediaan", headers, rows, metadata);
        }
        else if (job.ReportType.Equals("AuditLog", StringComparison.OrdinalIgnoreCase))
        {
            var logs = await _context.AuditLogs
                .AsNoTracking()
                .OrderByDescending(a => a.TimestampUtc)
                .Take(2000)
                .ToListAsync(cancellationToken);

            var headers = new List<string> { "Waktu (UTC)", "Aksi", "Tipe Entitas", "ID Entitas", "User", "Alasan" };
            var rows = logs.Select(a => (IReadOnlyList<object?>)new object?[]
            {
                a.TimestampUtc,
                a.Action,
                a.EntityType,
                a.EntityId,
                a.UserId?.ToString() ?? "Sistem",
                a.Reason ?? "-"
            }).ToList();

            return ("Laporan Riwayat Audit", headers, rows, metadata);
        }
        else
        {
            // Default generic fallback
            var headers = new List<string> { "Parameter", "Nilai" };
            var rows = new List<IReadOnlyList<object?>>
            {
                new object?[] { "ReportType", job.ReportType },
                new object?[] { "Filter", job.FilterPayloadJson }
            };
            return ($"Laporan {job.ReportType}", headers, rows, metadata);
        }
    }

    private static ExportJobDto MapToDto(ExportJob job)
    {
        return new ExportJobDto
        {
            Id = job.Id,
            ReportType = job.ReportType,
            Format = job.Format,
            Status = job.Status,
            FileName = job.FileName,
            RowCount = job.RowCount,
            FileSizeBytes = job.FileSizeBytes,
            ErrorMessage = job.ErrorMessage,
            CreatedAtUtc = job.CreatedAtUtc,
            CompletedAtUtc = job.CompletedAtUtc,
            ExpiresAtUtc = job.ExpiresAtUtc
        };
    }
}
