using Wms.Domain.Entities.Identity;
using Wms.Domain.Entities.Master;
using Wms.Domain.Enums;

namespace Wms.Domain.Entities.Inventory;

/// <summary>
/// Tabel ledger pergerakan stok bersifat append-only.
/// Mencatat setiap perpindahan/perubahan saldo persediaan secara kronologis.
/// </summary>
public class StockMovement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string MovementNumber { get; set; } = string.Empty;
    public MovementType MovementType { get; set; }

    public Guid ProductId { get; set; }
    public Guid WarehouseId { get; set; }
    public Guid WarehouseLocationId { get; set; }
    public string? BatchNumber { get; set; }
    public StockStatus StockStatus { get; set; } = StockStatus.Available;

    public decimal Quantity { get; set; }
    public MovementDirection Direction { get; set; }

    public decimal BalanceBefore { get; set; }
    public decimal BalanceAfter { get; set; }

    public decimal UnitCost { get; set; }
    public decimal TotalValue { get; set; }

    public SourceDocumentType SourceDocumentType { get; set; }
    public Guid SourceDocumentId { get; set; }
    public string SourceDocumentNumber { get; set; } = string.Empty;

    public string? Reason { get; set; }

    public Guid UserId { get; set; }
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;

    public MovementStatus Status { get; set; } = MovementStatus.Posted;
    public Guid? ReversedByMovementId { get; set; }
    public DateTime? ReversedAtUtc { get; set; }

    public Product Product { get; set; } = null!;
    public Warehouse Warehouse { get; set; } = null!;
    public WarehouseLocation WarehouseLocation { get; set; } = null!;
    public ApplicationUser User { get; set; } = null!;
    public StockMovement? ReversedByMovement { get; set; }
}

