using Wms.Domain.Entities.Master;

namespace Wms.Domain.Entities.Inventory;

/// <summary>
/// Menyimpan valuasi persediaan rata-rata bergerak (moving average) per barang per gudang.
/// Sesuai bagian 9.3 spesifikasi WMS.
/// </summary>
public class InventoryValuation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProductId { get; set; }
    public Guid WarehouseId { get; set; }
    public decimal OnHandQty { get; set; }
    public decimal OnHandValue { get; set; }
    public decimal AvgCost { get; set; }
    public decimal LastCost { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public Product Product { get; set; } = null!;
    public Warehouse Warehouse { get; set; } = null!;
}

