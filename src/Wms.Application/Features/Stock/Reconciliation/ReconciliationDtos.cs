using Wms.Domain.Enums;

namespace Wms.Application.Features.Stock.Reconciliation;

public record ReconciliationReport
{
    public DateTime RunAtUtc { get; init; } = DateTime.UtcNow;
    public int TotalBalancesChecked { get; init; }
    public int DiscrepancyCount { get; init; }
    public bool IsConsistent => DiscrepancyCount == 0;
    public IReadOnlyList<StockDiscrepancyDto> Discrepancies { get; init; } = Array.Empty<StockDiscrepancyDto>();
}

public record StockDiscrepancyDto
{
    public Guid ProductId { get; init; }
    public string ProductSku { get; init; } = string.Empty;
    public string ProductName { get; init; } = string.Empty;
    public Guid WarehouseId { get; init; }
    public string WarehouseName { get; init; } = string.Empty;
    public Guid WarehouseLocationId { get; init; }
    public string WarehouseLocationCode { get; init; } = string.Empty;
    public StockStatus Status { get; init; }
    public string? BatchNumber { get; init; }

    public decimal BalanceOnHand { get; init; }
    public decimal LedgerOnHand { get; init; }
    public decimal Difference => BalanceOnHand - LedgerOnHand;

    public string Reason { get; init; } = string.Empty;
}

