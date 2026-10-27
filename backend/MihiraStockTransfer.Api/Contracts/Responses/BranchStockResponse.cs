namespace MihiraStockTransfer.Api.Contracts.Responses;

public record BranchStockResponse(
    int BranchId,
    string BranchName,
    IReadOnlyList<StockItemResponse> Items);
