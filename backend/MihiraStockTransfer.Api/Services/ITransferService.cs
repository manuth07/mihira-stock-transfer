using MihiraStockTransfer.Api.Contracts.Requests;
using MihiraStockTransfer.Api.Contracts.Responses;

namespace MihiraStockTransfer.Api.Services;

public interface ITransferService
{
    Task<TransferResult> CreateTransferAsync(
        CreateTransferRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TransferResponse>> GetTransferHistoryAsync(
        CancellationToken cancellationToken = default);
}
