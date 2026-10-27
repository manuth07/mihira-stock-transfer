using Microsoft.AspNetCore.Mvc;
using MihiraStockTransfer.Api.Contracts.Requests;
using MihiraStockTransfer.Api.Contracts.Responses;
using MihiraStockTransfer.Api.Services;

namespace MihiraStockTransfer.Api.Controllers;

[ApiController]
[Route("transfers")]
public class TransfersController : ControllerBase
{
    private readonly ITransferService _transferService;

    public TransfersController(ITransferService transferService)
    {
        _transferService = transferService;
    }

    [HttpPost]
    [ProducesResponseType(typeof(TransferResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<TransferResponse>> CreateTransfer(
        [FromBody] CreateTransferRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _transferService.CreateTransferAsync(request, cancellationToken);

        if (result.IsSuccess)
        {
            return Ok(result.Value);
        }

        return result.ErrorType switch
        {
            TransferErrorType.InvalidRequest => Problem(
                title: "Invalid transfer request",
                statusCode: StatusCodes.Status400BadRequest,
                detail: result.ErrorMessage),

            TransferErrorType.NotFound => Problem(
                title: "Resource not found",
                statusCode: StatusCodes.Status404NotFound,
                detail: result.ErrorMessage),

            TransferErrorType.InventoryConflict => Problem(
                title: "Inventory conflict",
                statusCode: StatusCodes.Status409Conflict,
                detail: result.ErrorMessage),

            TransferErrorType.DatabaseContention => Problem(
                title: "Service temporarily unavailable",
                statusCode: StatusCodes.Status503ServiceUnavailable,
                detail: result.ErrorMessage),

            _ => Problem(
                title: "An unexpected error occurred",
                statusCode: StatusCodes.Status500InternalServerError,
                detail: result.ErrorMessage)
        };
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<TransferResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TransferResponse>>> GetTransferHistory(
        CancellationToken cancellationToken)
    {
        var history = await _transferService.GetTransferHistoryAsync(cancellationToken);
        return Ok(history);
    }
}
