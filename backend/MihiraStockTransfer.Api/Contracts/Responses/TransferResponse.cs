namespace MihiraStockTransfer.Api.Contracts.Responses;

public record TransferResponse(
    int Id,
    int FromBranchId,
    string FromBranchName,
    int ToBranchId,
    string ToBranchName,
    int ProductId,
    string ProductName,
    string PackageSize,
    int Quantity,
    DateTime CreatedAtUtc);
