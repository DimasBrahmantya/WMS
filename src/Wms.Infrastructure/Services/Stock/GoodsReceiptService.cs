using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Interfaces;
using Wms.Application.Common.Models;
using Wms.Application.Features.Stock.DocumentNumbering;
using Wms.Application.Features.Stock.GoodsReceipts;
using Wms.Application.Features.Stock.Postings;
using Wms.Domain.Common;
using Wms.Domain.Entities.Transactions;
using Wms.Domain.Enums;

namespace Wms.Infrastructure.Services.Stock;

public class GoodsReceiptService : IGoodsReceiptService
{
    private readonly IWmsDbContext _dbContext;
    private readonly IDocumentSequenceService _sequenceService;
    private readonly IStockPostingService _postingService;
    private readonly IUserContext _userContext;
    private readonly IValidator<CreateGoodsReceiptCommand> _createValidator;
    private readonly IValidator<UpdateGoodsReceiptCommand> _updateValidator;

    public GoodsReceiptService(
        IWmsDbContext dbContext,
        IDocumentSequenceService sequenceService,
        IStockPostingService postingService,
        IUserContext userContext,
        IValidator<CreateGoodsReceiptCommand> createValidator,
        IValidator<UpdateGoodsReceiptCommand> updateValidator)
    {
        _dbContext = dbContext;
        _sequenceService = sequenceService;
        _postingService = postingService;
        _userContext = userContext;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<PaginatedList<GoodsReceiptDto>> GetGoodsReceiptsAsync(
        GetGoodsReceiptsQuery query,
        CancellationToken cancellationToken = default)
    {
        var dbQuery = _dbContext.GoodsReceipts
            .Include(r => r.Warehouse)
            .Include(r => r.Supplier)
            .Include(r => r.CreatedByUser)
            .Include(r => r.PostedByUser)
            .Include(r => r.ReversedByUser)
            .Include(r => r.Lines)
            .AsNoTracking();

        if (query.WarehouseId.HasValue)
        {
            dbQuery = dbQuery.Where(r => r.WarehouseId == query.WarehouseId.Value);
        }

        if (query.Status.HasValue)
        {
            dbQuery = dbQuery.Where(r => r.Status == query.Status.Value);
        }

        if (query.StartDate.HasValue)
        {
            dbQuery = dbQuery.Where(r => r.DocumentDate >= query.StartDate.Value);
        }

        if (query.EndDate.HasValue)
        {
            dbQuery = dbQuery.Where(r => r.DocumentDate <= query.EndDate.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            var search = query.SearchTerm.Trim().ToLower();
            dbQuery = dbQuery.Where(r =>
                r.ReceiptNumber.ToLower().Contains(search) ||
                (r.PurchaseOrderNumber != null && r.PurchaseOrderNumber.ToLower().Contains(search)) ||
                (r.Supplier != null && r.Supplier.Name.ToLower().Contains(search)));
        }

        var totalCount = await dbQuery.CountAsync(cancellationToken);

        var items = await dbQuery
            .OrderByDescending(r => r.DocumentDate)
            .ThenByDescending(r => r.CreatedAtUtc)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(r => new GoodsReceiptDto
            {
                Id = r.Id,
                ReceiptNumber = r.ReceiptNumber,
                DocumentDate = r.DocumentDate,
                WarehouseId = r.WarehouseId,
                WarehouseName = r.Warehouse.Name,
                SupplierId = r.SupplierId,
                SupplierName = r.Supplier != null ? r.Supplier.Name : null,
                PurchaseOrderNumber = r.PurchaseOrderNumber,
                Notes = r.Notes,
                Status = r.Status,
                CreatedByUserId = r.CreatedByUserId,
                CreatedByUserName = r.CreatedByUser.FullName,
                CreatedAtUtc = r.CreatedAtUtc,
                PostedByUserId = r.PostedByUserId,
                PostedByUserName = r.PostedByUser != null ? r.PostedByUser.FullName : null,
                PostedAtUtc = r.PostedAtUtc,
                ReversedByUserId = r.ReversedByUserId,
                ReversedByUserName = r.ReversedByUser != null ? r.ReversedByUser.FullName : null,
                ReversedAtUtc = r.ReversedAtUtc,
                ReversalReason = r.ReversalReason,
                TotalQuantity = r.Lines.Sum(l => l.Quantity),
                TotalAmount = r.Lines.Sum(l => l.TotalCost)
            })
            .ToListAsync(cancellationToken);

        return new PaginatedList<GoodsReceiptDto>(items, totalCount, query.Page, query.PageSize);
    }

    public async Task<Result<GoodsReceiptDto>> GetGoodsReceiptByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var receipt = await _dbContext.GoodsReceipts
            .Include(r => r.Warehouse)
            .Include(r => r.Supplier)
            .Include(r => r.CreatedByUser)
            .Include(r => r.PostedByUser)
            .Include(r => r.ReversedByUser)
            .Include(r => r.Lines)
                .ThenInclude(l => l.Product)
                    .ThenInclude(p => p.Unit)
            .Include(r => r.Lines)
                .ThenInclude(l => l.WarehouseLocation)
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        if (receipt == null)
        {
            return Result<GoodsReceiptDto>.Failure("Dokumen penerimaan barang tidak ditemukan.");
        }

        var dto = new GoodsReceiptDto
        {
            Id = receipt.Id,
            ReceiptNumber = receipt.ReceiptNumber,
            DocumentDate = receipt.DocumentDate,
            WarehouseId = receipt.WarehouseId,
            WarehouseName = receipt.Warehouse.Name,
            SupplierId = receipt.SupplierId,
            SupplierName = receipt.Supplier?.Name,
            PurchaseOrderId = receipt.PurchaseOrderId,
            PurchaseOrderNumber = receipt.PurchaseOrderNumber,
            Notes = receipt.Notes,
            Status = receipt.Status,
            CreatedByUserId = receipt.CreatedByUserId,
            CreatedByUserName = receipt.CreatedByUser.FullName,
            CreatedAtUtc = receipt.CreatedAtUtc,
            PostedByUserId = receipt.PostedByUserId,
            PostedByUserName = receipt.PostedByUser?.FullName,
            PostedAtUtc = receipt.PostedAtUtc,
            ReversedByUserId = receipt.ReversedByUserId,
            ReversedByUserName = receipt.ReversedByUser?.FullName,
            ReversedAtUtc = receipt.ReversedAtUtc,
            ReversalReason = receipt.ReversalReason,
            TotalQuantity = receipt.Lines.Sum(l => l.Quantity),
            TotalAmount = receipt.Lines.Sum(l => l.TotalCost),
            Lines = receipt.Lines.Select(l => new GoodsReceiptLineDto
            {
                Id = l.Id,
                GoodsReceiptId = l.GoodsReceiptId,
                PurchaseOrderLineId = l.PurchaseOrderLineId,
                ProductId = l.ProductId,
                ProductSku = l.Product.Sku,
                ProductName = l.Product.Name,
                UnitName = l.Product.Unit.Name,
                WarehouseLocationId = l.WarehouseLocationId,
                WarehouseLocationCode = l.WarehouseLocation.Code,
                WarehouseLocationName = l.WarehouseLocation.Name,
                Quantity = l.Quantity,
                UnitCost = l.UnitCost,
                TotalCost = l.TotalCost,
                BatchNumber = l.BatchNumber,
                ExpiryDate = l.ExpiryDate,
                Notes = l.Notes
            }).ToList()
        };

        return Result<GoodsReceiptDto>.Success(dto);
    }

    public async Task<Result<GoodsReceiptDto>> CreateDraftAsync(
        CreateGoodsReceiptCommand command,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await _createValidator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Result<GoodsReceiptDto>.Failure(validationResult.Errors.Select(e => e.ErrorMessage));
        }

        var receiptNumber = await _sequenceService.GetNextDocumentNumberAsync("GR", command.DocumentDate, cancellationToken);
        var now = DateTime.UtcNow;
        var userId = _userContext.UserId ?? Guid.Empty;

        var receipt = new GoodsReceipt
        {
            Id = Guid.NewGuid(),
            ReceiptNumber = receiptNumber,
            DocumentDate = DateTime.SpecifyKind(command.DocumentDate, DateTimeKind.Utc),
            WarehouseId = command.WarehouseId,
            SupplierId = command.SupplierId,
            PurchaseOrderId = command.PurchaseOrderId,
            PurchaseOrderNumber = command.PurchaseOrderNumber,
            Notes = command.Notes,
            Status = DocumentStatus.Draft,
            CreatedByUserId = userId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        foreach (var lineDto in command.Lines)
        {
            var line = new GoodsReceiptLine
            {
                Id = Guid.NewGuid(),
                GoodsReceiptId = receipt.Id,
                PurchaseOrderLineId = lineDto.PurchaseOrderLineId,
                ProductId = lineDto.ProductId,
                WarehouseLocationId = lineDto.WarehouseLocationId,
                Quantity = lineDto.Quantity,
                UnitCost = lineDto.UnitCost,
                TotalCost = Math.Round(lineDto.Quantity * lineDto.UnitCost, 4, MidpointRounding.AwayFromZero),
                BatchNumber = lineDto.BatchNumber,
                ExpiryDate = lineDto.ExpiryDate.HasValue ? DateTime.SpecifyKind(lineDto.ExpiryDate.Value, DateTimeKind.Utc) : null,
                Notes = lineDto.Notes
            };
            receipt.Lines.Add(line);
        }

        _dbContext.GoodsReceipts.Add(receipt);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return await GetGoodsReceiptByIdAsync(receipt.Id, cancellationToken);
    }

    public async Task<Result> UpdateDraftAsync(
        UpdateGoodsReceiptCommand command,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await _updateValidator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Result.Failure(validationResult.Errors.Select(e => e.ErrorMessage));
        }

        var receipt = await _dbContext.GoodsReceipts
            .Include(r => r.Lines)
            .FirstOrDefaultAsync(r => r.Id == command.Id, cancellationToken);

        if (receipt == null)
        {
            return Result.Failure("Dokumen penerimaan barang tidak ditemukan.");
        }

        if (receipt.Status != DocumentStatus.Draft)
        {
            return Result.Failure("Hanya dokumen berstatus 'Draft' yang dapat diedit.");
        }

        receipt.DocumentDate = DateTime.SpecifyKind(command.DocumentDate, DateTimeKind.Utc);
        receipt.WarehouseId = command.WarehouseId;
        receipt.SupplierId = command.SupplierId;
        receipt.PurchaseOrderId = command.PurchaseOrderId;
        receipt.PurchaseOrderNumber = command.PurchaseOrderNumber;
        receipt.Notes = command.Notes;
        receipt.UpdatedAtUtc = DateTime.UtcNow;

        _dbContext.GoodsReceiptLines.RemoveRange(receipt.Lines);

        foreach (var lineDto in command.Lines)
        {
            receipt.Lines.Add(new GoodsReceiptLine
            {
                Id = Guid.NewGuid(),
                GoodsReceiptId = receipt.Id,
                PurchaseOrderLineId = lineDto.PurchaseOrderLineId,
                ProductId = lineDto.ProductId,
                WarehouseLocationId = lineDto.WarehouseLocationId,
                Quantity = lineDto.Quantity,
                UnitCost = lineDto.UnitCost,
                TotalCost = Math.Round(lineDto.Quantity * lineDto.UnitCost, 4, MidpointRounding.AwayFromZero),
                BatchNumber = lineDto.BatchNumber,
                ExpiryDate = lineDto.ExpiryDate.HasValue ? DateTime.SpecifyKind(lineDto.ExpiryDate.Value, DateTimeKind.Utc) : null,
                Notes = lineDto.Notes
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> DeleteDraftAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var receipt = await _dbContext.GoodsReceipts
            .Include(r => r.Lines)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        if (receipt == null)
        {
            return Result.Failure("Dokumen penerimaan barang tidak ditemukan.");
        }

        if (receipt.Status != DocumentStatus.Draft)
        {
            return Result.Failure("Hanya dokumen berstatus 'Draft' yang dapat dihapus.");
        }

        _dbContext.GoodsReceipts.Remove(receipt);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result<Guid>> PostAsync(
        Guid id,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var userId = _userContext.UserId ?? Guid.Empty;
        return await _postingService.PostGoodsReceiptAsync(id, userId, idempotencyKey, cancellationToken);
    }

    public async Task<Result<Guid>> ReverseAsync(
        Guid id,
        string reason,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var userId = _userContext.UserId ?? Guid.Empty;
        return await _postingService.ReverseGoodsReceiptAsync(id, userId, reason, idempotencyKey, cancellationToken);
    }
}

