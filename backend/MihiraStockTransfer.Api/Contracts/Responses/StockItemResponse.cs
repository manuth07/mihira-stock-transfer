namespace MihiraStockTransfer.Api.Contracts.Responses;

public record StockItemResponse(
    int ProductId,
    string ProductName,
    string PackageSize,
    int Quantity);
