using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wms.Application.Features.SalesOrders;
using Wms.Domain.Enums;
using Wms.Web.Authentication;

namespace Wms.Web.Controllers.Api.v1;

[ApiController]
[Route("api/v1/sales-orders")]
[Authorize(AuthenticationSchemes = ApiKeyAuthenticationOptions.SchemeName)]
public class SalesOrdersApiController : ControllerBase
{
    private readonly ISalesOrderService _salesOrderService;

    public SalesOrdersApiController(ISalesOrderService salesOrderService)
    {
        _salesOrderService = salesOrderService;
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetOrderById(Guid id)
    {
        if (!User.HasClaim("scope", ApiKeyScope.SalesOrdersRead))
        {
            return Forbid();
        }

        var order = await _salesOrderService.GetSalesOrderByIdAsync(id);
        if (order == null)
        {
            return NotFound(new { success = false, message = "Sales Order tidak ditemukan." });
        }

        return Ok(new { success = true, data = order });
    }

    [HttpPost]
    public async Task<IActionResult> CreateOrder([FromBody] CreateSalesOrderCommand command, [FromQuery] bool autoConfirm = true)
    {
        if (!User.HasClaim("scope", ApiKeyScope.SalesOrdersWrite))
        {
            return Forbid();
        }

        var apiKeyIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var actorId = Guid.TryParse(apiKeyIdStr, out var parsedId) ? parsedId : Guid.NewGuid();

        var result = await _salesOrderService.CreateSalesOrderAsync(command, actorId);
        if (!result.Succeeded)
        {
            return BadRequest(new { success = false, errors = result.Errors });
        }

        var order = result.Data!;

        if (autoConfirm)
        {
            var confirmResult = await _salesOrderService.ConfirmSalesOrderAsync(order.Id, actorId);
            if (!confirmResult.Succeeded)
            {
                return Ok(new
                {
                    success = true,
                    data = order,
                    warning = $"Order berhasil dibuat tetapi gagal auto-confirm: {string.Join(", ", confirmResult.Errors)}"
                });
            }

            order = await _salesOrderService.GetSalesOrderByIdAsync(order.Id);
        }

        return CreatedAtAction(nameof(GetOrderById), new { id = order!.Id }, new { success = true, data = order });
    }
}

