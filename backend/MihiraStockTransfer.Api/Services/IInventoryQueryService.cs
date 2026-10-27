using MihiraStockTransfer.Api.Contracts.Responses;

namespace MihiraStockTransfer.Api.Services;

public interface IInventoryQueryService
{
    Task<IReadOnlyList<BranchResponse>> GetBranchesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProductResponse>> GetProductsAsync(CancellationToken cancellationToken = default);

    Task<BranchStockResponse?> GetBranchStockAsync(int branchId, CancellationToken cancellationToken = default);
}
