using Microsoft.AspNetCore.Mvc;
using MihiraStockTransfer.Api.Contracts.Responses;
using MihiraStockTransfer.Api.Services;

namespace MihiraStockTransfer.Api.Controllers;

[ApiController]
[Route("branches")]
public class BranchesController : ControllerBase
{
    private readonly IInventoryQueryService _queryService;

    public BranchesController(IInventoryQueryService queryService)
    {
        _queryService = queryService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<BranchResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<BranchResponse>>> GetBranches(
        CancellationToken cancellationToken)
    {
        var branches = await _queryService.GetBranchesAsync(cancellationToken);
        return Ok(branches);
    }

    [HttpGet("{id:int}/stock")]
    [ProducesResponseType(typeof(BranchStockResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BranchStockResponse>> GetBranchStock(
        int id,
        CancellationToken cancellationToken)
    {
        var stock = await _queryService.GetBranchStockAsync(id, cancellationToken);
        if (stock is null)
        {
            return Problem(
                title: "Branch not found",
                statusCode: StatusCodes.Status404NotFound,
                detail: $"Branch with ID {id} does not exist.");
        }

        return Ok(stock);
    }
}
