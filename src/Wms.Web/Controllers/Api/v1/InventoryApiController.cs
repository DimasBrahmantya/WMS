using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Interfaces;
using Wms.Domain.Enums;
using Wms.Web.Authentication;

namespace Wms.Web.Controllers.Api.v1;

[ApiController]
[Route("api/v1/inventory")]
[Authorize(AuthenticationSchemes = ApiKeyAuthenticationOptions.SchemeName)]
public class InventoryApiController : ControllerBase
{
    private readonly IWmsDbContext _context;

    public InventoryApiController(IWmsDbContext context)
    {
        _context = context;
    }

    [HttpGet("balance")]
    public async Task<IActionResult> GetBalances(
        [FromQuery] Guid? warehouseId,
        [FromQuery] Guid? productId,
        [FromQuery] string? batchNumber,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        if (!User.HasClaim("scope", ApiKeyScope.InventoryRead))
        {
            return Forbid();
        }

        var baseQuery = _context.InventoryBalances
            .Include(b => b.Product)
            .Include(b => b.Warehouse)
            .Include(b => b.WarehouseLocation)
            .AsNoTracking();

        if (warehouseId.HasValue)
            baseQuery = baseQuery.Where(b => b.WarehouseId == warehouseId.Value);

        if (productId.HasValue)
            baseQuery = baseQuery.Where(b => b.ProductId == productId.Value);

        if (!string.IsNullOrWhiteSpace(batchNumber))
            baseQuery = baseQuery.Where(b => b.BatchNumber == batchNumber.Trim());

        var totalCount = await baseQuery.CountAsync();

        var balances = await baseQuery
            .OrderBy(b => b.Warehouse.Name)
            .ThenBy(b => b.Product.Sku)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(b => new
            {
                b.Id,
                b.ProductId,
                ProductSku = b.Product.Sku,
                ProductName = b.Product.Name,
                b.WarehouseId,
                WarehouseCode = b.Warehouse.Code,
                WarehouseName = b.Warehouse.Name,
                b.WarehouseLocationId,
                LocationCode = b.WarehouseLocation.Code,
                b.BatchNumber,
                Status = b.Status.ToString(),
                b.OnHand,
                b.Reserved,
                Available = b.OnHand - b.Reserved,
                b.UpdatedAtUtc
            })
            .ToListAsync();

        return Ok(new
        {
            success = true,
            page,
            pageSize,
            totalCount,
            data = balances
        });
    }
}

