using Wms.Domain.Entities.Master;

namespace Wms.Domain.Entities.Transactions;

public class StockOutLine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StockOutId { get; set; }
    public Guid ProductId { get; set; }
    public Guid WarehouseLocationId { get; set; }

    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; } // Diisi saat posting berdasarkan rata-rata bergerak saat itu
    public decimal TotalCost { get; set; }

    public string? BatchNumber { get; set; }
    public string? Notes { get; set; }

    public StockOut StockOut { get; set; } = null!;
    public Product Product { get; set; } = null!;
    public WarehouseLocation WarehouseLocation { get; set; } = null!;
}

