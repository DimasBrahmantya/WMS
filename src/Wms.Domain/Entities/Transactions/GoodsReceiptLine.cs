using Wms.Domain.Entities.Master;

namespace Wms.Domain.Entities.Transactions;

public class GoodsReceiptLine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GoodsReceiptId { get; set; }
    public Guid ProductId { get; set; }
    public Guid WarehouseLocationId { get; set; }

    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }

    public string? BatchNumber { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string? Notes { get; set; }

    public GoodsReceipt GoodsReceipt { get; set; } = null!;
    public Product Product { get; set; } = null!;
    public WarehouseLocation WarehouseLocation { get; set; } = null!;
}

