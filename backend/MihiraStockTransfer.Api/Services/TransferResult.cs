using MihiraStockTransfer.Api.Contracts.Responses;

namespace MihiraStockTransfer.Api.Services;

public enum TransferErrorType
{
    InvalidRequest,
    NotFound,
    InventoryConflict,
    DatabaseContention
}

public class TransferResult
{
    private TransferResult(bool isSuccess, TransferResponse? value, TransferErrorType? errorType, string? errorMessage)
    {
        IsSuccess = isSuccess;
        Value = value;
        ErrorType = errorType;
        ErrorMessage = errorMessage;
    }

    public bool IsSuccess { get; }

    public TransferResponse? Value { get; }

    public TransferErrorType? ErrorType { get; }

    public string? ErrorMessage { get; }

    public static TransferResult Success(TransferResponse value) =>
        new(true, value, null, null);

    public static TransferResult InvalidRequest(string message) =>
        new(false, null, TransferErrorType.InvalidRequest, message);

    public static TransferResult NotFound(string message) =>
        new(false, null, TransferErrorType.NotFound, message);

    public static TransferResult InventoryConflict(string message) =>
        new(false, null, TransferErrorType.InventoryConflict, message);

    public static TransferResult DatabaseContention(string message) =>
        new(false, null, TransferErrorType.DatabaseContention, message);
}
