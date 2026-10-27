using Microsoft.AspNetCore.Mvc;
using MihiraStockTransfer.Api.Contracts.Responses;
using MihiraStockTransfer.Api.Services;

namespace MihiraStockTransfer.Api.Controllers;

[ApiController]
[Route("products")]
public class ProductsController : ControllerBase
{
    private readonly IInventoryQueryService _queryService;

    public ProductsController(IInventoryQueryService queryService)
    {
        _queryService = queryService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ProductResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ProductResponse>>> GetProducts(
        CancellationToken cancellationToken)
    {
        var products = await _queryService.GetProductsAsync(cancellationToken);
        return Ok(products);
    }
}
