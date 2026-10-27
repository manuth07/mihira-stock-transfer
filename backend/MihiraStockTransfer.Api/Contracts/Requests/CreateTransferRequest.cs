namespace MihiraStockTransfer.Api.Contracts.Requests;

public record CreateTransferRequest(
    int FromBranchId,
    int ToBranchId,
    int ProductId,
    int Quantity);
