using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Interfaces;
using Wms.Application.Common.Models;
using Wms.Application.Features.Stock.DocumentNumbering;
using Wms.Application.Features.Stock.Postings;
using Wms.Application.Features.Stock.StockOuts;
using Wms.Domain.Common;
using Wms.Domain.Entities.Transactions;
using Wms.Domain.Enums;

namespace Wms.Infrastructure.Services.Stock;

public class StockOutService : IStockOutService
{
    private readonly IWmsDbContext _dbContext;
    private readonly IDocumentSequenceService _sequenceService;
    private readonly IStockPostingService _postingService;
    private readonly IUserContext _userContext;
    private readonly IValidator<CreateStockOutCommand> _createValidator;
    private readonly IValidator<UpdateStockOutCommand> _updateValidator;

    public StockOutService(
        IWmsDbContext dbContext,
        IDocumentSequenceService sequenceService,
        IStockPostingService postingService,
        IUserContext userContext,
        IValidator<CreateStockOutCommand> createValidator,
        IValidator<UpdateStockOutCommand> updateValidator)
    {
        _dbContext = dbContext;
        _sequenceService = sequenceService;
        _postingService = postingService;
        _userContext = userContext;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<PaginatedList<StockOutDto>> GetStockOutsAsync(
        GetStockOutsQuery query,
        CancellationToken cancellationToken = default)
    {
        var dbQuery = _dbContext.StockOuts
            .Include(s => s.Warehouse)
            .Include(s => s.Customer)
            .Include(s => s.CreatedByUser)
            .Include(s => s.PostedByUser)
            .Include(s => s.ReversedByUser)
            .Include(s => s.Lines)
            .AsNoTracking();

        if (query.WarehouseId.HasValue)
        {
            dbQuery = dbQuery.Where(s => s.WarehouseId == query.WarehouseId.Value);
        }

        if (query.Status.HasValue)
        {
            dbQuery = dbQuery.Where(s => s.Status == query.Status.Value);
        }

        if (query.IssueType.HasValue)
        {
            dbQuery = dbQuery.Where(s => s.IssueType == query.IssueType.Value);
        }

        if (query.StartDate.HasValue)
        {
            dbQuery = dbQuery.Where(s => s.DocumentDate >= query.StartDate.Value);
        }

        if (query.EndDate.HasValue)
        {
            dbQuery = dbQuery.Where(s => s.DocumentDate <= query.EndDate.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            var search = query.SearchTerm.Trim().ToLower();
            dbQuery = dbQuery.Where(s =>
                s.IssueNumber.ToLower().Contains(search) ||
                (s.Recipient != null && s.Recipient.ToLower().Contains(search)) ||
                (s.Customer != null && s.Customer.Name.ToLower().Contains(search)));
        }

        var totalCount = await dbQuery.CountAsync(cancellationToken);

        var items = await dbQuery
            .OrderByDescending(s => s.DocumentDate)
            .ThenByDescending(s => s.CreatedAtUtc)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(s => new StockOutDto
            {
                Id = s.Id,
                IssueNumber = s.IssueNumber,
                DocumentDate = s.DocumentDate,
                WarehouseId = s.WarehouseId,
                WarehouseName = s.Warehouse.Name,
                CustomerId = s.CustomerId,
                CustomerName = s.Customer != null ? s.Customer.Name : null,
                Recipient = s.Recipient,
                IssueType = s.IssueType,
                Notes = s.Notes,
                Status = s.Status,
                CreatedByUserId = s.CreatedByUserId,
                CreatedByUserName = s.CreatedByUser.FullName,
                CreatedAtUtc = s.CreatedAtUtc,
                PostedByUserId = s.PostedByUserId,
                PostedByUserName = s.PostedByUser != null ? s.PostedByUser.FullName : null,
                PostedAtUtc = s.PostedAtUtc,
                ReversedByUserId = s.ReversedByUserId,
                ReversedByUserName = s.ReversedByUser != null ? s.ReversedByUser.FullName : null,
                ReversedAtUtc = s.ReversedAtUtc,
                ReversalReason = s.ReversalReason,
                TotalQuantity = s.Lines.Sum(l => l.Quantity),
                TotalAmount = s.Lines.Sum(l => l.TotalCost)
            })
            .ToListAsync(cancellationToken);

        return new PaginatedList<StockOutDto>(items, totalCount, query.Page, query.PageSize);
    }

    public async Task<Result<StockOutDto>> GetStockOutByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var stockOut = await _dbContext.StockOuts
            .Include(s => s.Warehouse)
            .Include(s => s.Customer)
            .Include(s => s.CreatedByUser)
            .Include(s => s.PostedByUser)
            .Include(s => s.ReversedByUser)
            .Include(s => s.Lines)
                .ThenInclude(l => l.Product)
                    .ThenInclude(p => p.Unit)
            .Include(s => s.Lines)
                .ThenInclude(l => l.WarehouseLocation)
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (stockOut == null)
        {
            return Result<StockOutDto>.Failure("Dokumen pengeluaran barang tidak ditemukan.");
        }

        var lines = new List<StockOutLineDto>();
        foreach (var l in stockOut.Lines)
        {
            var avail = await GetAvailableStockAsync(l.WarehouseLocationId, l.ProductId, l.BatchNumber, cancellationToken);
            lines.Add(new StockOutLineDto
            {
                Id = l.Id,
                StockOutId = l.StockOutId,
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
                Notes = l.Notes,
                AvailableStock = avail
            });
        }

        var dto = new StockOutDto
        {
            Id = stockOut.Id,
            IssueNumber = stockOut.IssueNumber,
            DocumentDate = stockOut.DocumentDate,
            WarehouseId = stockOut.WarehouseId,
            WarehouseName = stockOut.Warehouse.Name,
            CustomerId = stockOut.CustomerId,
            CustomerName = stockOut.Customer?.Name,
            Recipient = stockOut.Recipient,
            IssueType = stockOut.IssueType,
            Notes = stockOut.Notes,
            Status = stockOut.Status,
            CreatedByUserId = stockOut.CreatedByUserId,
            CreatedByUserName = stockOut.CreatedByUser.FullName,
            CreatedAtUtc = stockOut.CreatedAtUtc,
            PostedByUserId = stockOut.PostedByUserId,
            PostedByUserName = stockOut.PostedByUser?.FullName,
            PostedAtUtc = stockOut.PostedAtUtc,
            ReversedByUserId = stockOut.ReversedByUserId,
            ReversedByUserName = stockOut.ReversedByUser?.FullName,
            ReversedAtUtc = stockOut.ReversedAtUtc,
            ReversalReason = stockOut.ReversalReason,
            TotalQuantity = stockOut.Lines.Sum(l => l.Quantity),
            TotalAmount = stockOut.Lines.Sum(l => l.TotalCost),
            Lines = lines
        };

        return Result<StockOutDto>.Success(dto);
    }

    public async Task<decimal> GetAvailableStockAsync(
        Guid warehouseLocationId,
        Guid productId,
        string? batchNumber = null,
        CancellationToken cancellationToken = default)
    {
        var balance = await _dbContext.InventoryBalances
            .AsNoTracking()
            .FirstOrDefaultAsync(b =>
                b.WarehouseLocationId == warehouseLocationId &&
                b.ProductId == productId &&
                b.Status == StockStatus.Available &&
                b.BatchNumber == batchNumber, cancellationToken);

        return balance?.Available ?? 0m;
    }

    public async Task<Result<StockOutDto>> CreateDraftAsync(
        CreateStockOutCommand command,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await _createValidator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Result<StockOutDto>.Failure(validationResult.Errors.Select(e => e.ErrorMessage));
        }

        var issueNumber = await _sequenceService.GetNextDocumentNumberAsync("GI", command.DocumentDate, cancellationToken);
        var now = DateTime.UtcNow;
        var userId = _userContext.UserId ?? Guid.Empty;

        var stockOut = new StockOut
        {
            Id = Guid.NewGuid(),
            IssueNumber = issueNumber,
            DocumentDate = DateTime.SpecifyKind(command.DocumentDate, DateTimeKind.Utc),
            WarehouseId = command.WarehouseId,
            CustomerId = command.CustomerId,
            Recipient = command.Recipient,
            IssueType = command.IssueType,
            Notes = command.Notes,
            Status = DocumentStatus.Draft,
            CreatedByUserId = userId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        foreach (var lineDto in command.Lines)
        {
            var line = new StockOutLine
            {
                Id = Guid.NewGuid(),
                StockOutId = stockOut.Id,
                ProductId = lineDto.ProductId,
                WarehouseLocationId = lineDto.WarehouseLocationId,
                Quantity = lineDto.Quantity,
                UnitCost = 0m,
                TotalCost = 0m,
                BatchNumber = lineDto.BatchNumber,
                Notes = lineDto.Notes
            };
            stockOut.Lines.Add(line);
        }

        _dbContext.StockOuts.Add(stockOut);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return await GetStockOutByIdAsync(stockOut.Id, cancellationToken);
    }

    public async Task<Result> UpdateDraftAsync(
        UpdateStockOutCommand command,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await _updateValidator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Result.Failure(validationResult.Errors.Select(e => e.ErrorMessage));
        }

        var stockOut = await _dbContext.StockOuts
            .Include(s => s.Lines)
            .FirstOrDefaultAsync(s => s.Id == command.Id, cancellationToken);

        if (stockOut == null)
        {
            return Result.Failure("Dokumen pengeluaran barang tidak ditemukan.");
        }

        if (stockOut.Status != DocumentStatus.Draft)
        {
            return Result.Failure("Hanya dokumen berstatus 'Draft' yang dapat diedit.");
        }

        stockOut.DocumentDate = DateTime.SpecifyKind(command.DocumentDate, DateTimeKind.Utc);
        stockOut.WarehouseId = command.WarehouseId;
        stockOut.CustomerId = command.CustomerId;
        stockOut.Recipient = command.Recipient;
        stockOut.IssueType = command.IssueType;
        stockOut.Notes = command.Notes;
        stockOut.UpdatedAtUtc = DateTime.UtcNow;

        _dbContext.StockOutLines.RemoveRange(stockOut.Lines);

        foreach (var lineDto in command.Lines)
        {
            stockOut.Lines.Add(new StockOutLine
            {
                Id = Guid.NewGuid(),
                StockOutId = stockOut.Id,
                ProductId = lineDto.ProductId,
                WarehouseLocationId = lineDto.WarehouseLocationId,
                Quantity = lineDto.Quantity,
                UnitCost = 0m,
                TotalCost = 0m,
                BatchNumber = lineDto.BatchNumber,
                Notes = lineDto.Notes
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> DeleteDraftAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var stockOut = await _dbContext.StockOuts
            .Include(s => s.Lines)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (stockOut == null)
        {
            return Result.Failure("Dokumen pengeluaran barang tidak ditemukan.");
        }

        if (stockOut.Status != DocumentStatus.Draft)
        {
            return Result.Failure("Hanya dokumen berstatus 'Draft' yang dapat dihapus.");
        }

        _dbContext.StockOuts.Remove(stockOut);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result<Guid>> PostAsync(
        Guid id,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var userId = _userContext.UserId ?? Guid.Empty;
        return await _postingService.PostStockOutAsync(id, userId, idempotencyKey, cancellationToken);
    }

    public async Task<Result<Guid>> ReverseAsync(
        Guid id,
        string reason,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var userId = _userContext.UserId ?? Guid.Empty;
        return await _postingService.ReverseStockOutAsync(id, userId, reason, idempotencyKey, cancellationToken);
    }
}

