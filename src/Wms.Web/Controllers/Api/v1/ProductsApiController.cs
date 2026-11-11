using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wms.Application.Features.Master.Products;
using Wms.Domain.Enums;
using Wms.Web.Authentication;

namespace Wms.Web.Controllers.Api.v1;

[ApiController]
[Route("api/v1/products")]
[Authorize(AuthenticationSchemes = ApiKeyAuthenticationOptions.SchemeName)]
public class ProductsApiController : ControllerBase
{
    private readonly IProductService _productService;

    public ProductsApiController(IProductService productService)
    {
        _productService = productService;
    }

    [HttpGet]
    public async Task<IActionResult> GetProducts([FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        if (!User.HasClaim("scope", ApiKeyScope.ProductsRead))
        {
            return Forbid();
        }

        var query = new GetProductsQuery
        {
            Search = search,
            IsActive = true,
            Page = page,
            PageSize = pageSize
        };

        var paged = await _productService.GetProductsAsync(query);

        return Ok(new
        {
            success = true,
            page = paged.PageNumber,
            pageSize = paged.PageSize,
            totalCount = paged.TotalCount,
            totalPages = paged.TotalPages,
            data = paged.Items
        });
    }
}

