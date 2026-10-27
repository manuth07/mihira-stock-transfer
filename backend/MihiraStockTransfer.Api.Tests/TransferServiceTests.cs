using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MihiraStockTransfer.Api.Contracts.Requests;
using MihiraStockTransfer.Api.Controllers;
using MihiraStockTransfer.Api.Data;
using MihiraStockTransfer.Api.Models;
using MihiraStockTransfer.Api.Services;
using MihiraStockTransfer.Api.Tests.Infrastructure;

namespace MihiraStockTransfer.Api.Tests;

/// <summary>
/// Service/database integration tests verifying stock transfer business rules,
/// transaction rollback guarantees, persistence invariants, and concurrency safety.
/// Each test executes against an isolated, temporary, file-backed SQLite database.
/// </summary>
public class TransferServiceTests
{
    // =========================================================================
    // Area 1: Valid Transfer
    // =========================================================================

    [Fact]
    public async Task ValidTransfer_DeductsSourceIncrementsDestinationPreservesTotalAndRecordsReceiptWithRecentUtcTimestamp()
    {
        await using var testDb = await SqliteTestDatabase.CreateAsync();
        using var context = testDb.CreateContext();
        var service = new TransferService(context, NullLogger<TransferService>.Instance);

        // Colombo has 50 Rice, Kandy has 20 Rice, Galle has 15 Rice. Total = 85.
        var beforeTransfer = DateTime.UtcNow;
        var request = new CreateTransferRequest(FromBranchId: 1, ToBranchId: 2, ProductId: 1, Quantity: 10);
        var result = await service.CreateTransferAsync(request);
        var afterTransfer = DateTime.UtcNow;

        // 1. Verify result outcome and receipt contract
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.True(result.Value.Id > 0);
        Assert.Equal(1, result.Value.FromBranchId);
        Assert.Equal("Colombo", result.Value.FromBranchName);
        Assert.Equal(2, result.Value.ToBranchId);
        Assert.Equal("Kandy", result.Value.ToBranchName);
        Assert.Equal(1, result.Value.ProductId);
        Assert.Equal("Rice", result.Value.ProductName);
        Assert.Equal("1 kg bag", result.Value.PackageSize);
        Assert.Equal(10, result.Value.Quantity);

        // 2. Verify server-generated UTC timestamp is recent and plausible
        Assert.True(result.Value.CreatedAtUtc >= beforeTransfer.AddSeconds(-1), "Timestamp must not precede request initiation.");
        Assert.True(result.Value.CreatedAtUtc <= afterTransfer.AddSeconds(1), "Timestamp must not succeed request completion.");

        // 3. Verify database persistence in fresh context
        using var verifyContext = testDb.CreateContext();
        var colomboRice = await verifyContext.Stocks.SingleAsync(s => s.BranchId == 1 && s.ProductId == 1);
        var kandyRice = await verifyContext.Stocks.SingleAsync(s => s.BranchId == 2 && s.ProductId == 1);
        var galleRice = await verifyContext.Stocks.SingleAsync(s => s.BranchId == 3 && s.ProductId == 1);

        // Exact deduction and increment
        Assert.Equal(40, colomboRice.Quantity);
        Assert.Equal(30, kandyRice.Quantity);
        Assert.Equal(15, galleRice.Quantity);

        // Total inventory across branches is strictly conserved
        Assert.Equal(85, colomboRice.Quantity + kandyRice.Quantity + galleRice.Quantity);

        // Exactly one transfer record created with matching receipt data
        var record = await verifyContext.Transfers.SingleAsync();
        Assert.Equal(result.Value.Id, record.Id);
        Assert.Equal(1, record.FromBranchId);
        Assert.Equal(2, record.ToBranchId);
        Assert.Equal(1, record.ProductId);
        Assert.Equal(10, record.Quantity);
        Assert.Equal(result.Value.CreatedAtUtc, record.CreatedAtUtc);
    }

    // =========================================================================
    // Area 2: Exact Available Stock
    // =========================================================================

    [Fact]
    public async Task ExactAvailableStock_TransferringAllAvailableStock_SucceedsLeavesZeroAndSatisfiesCheckConstraint()
    {
        await using var testDb = await SqliteTestDatabase.CreateAsync();
        using var context = testDb.CreateContext();
        var service = new TransferService(context, NullLogger<TransferService>.Instance);

        // Colombo has 50 Rice. Transfer 100% (50 units) to Galle (initial 15).
        var request = new CreateTransferRequest(FromBranchId: 1, ToBranchId: 3, ProductId: 1, Quantity: 50);
        var result = await service.CreateTransferAsync(request);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(50, result.Value.Quantity);

        using var verifyContext = testDb.CreateContext();
        var colomboStock = await verifyContext.Stocks.SingleAsync(s => s.BranchId == 1 && s.ProductId == 1);
        var galleStock = await verifyContext.Stocks.SingleAsync(s => s.BranchId == 3 && s.ProductId == 1);
        var kandyStock = await verifyContext.Stocks.SingleAsync(s => s.BranchId == 2 && s.ProductId == 1);

        // Source is at exactly 0; database constraint CK_Stock_Quantity_NonNegative (Quantity >= 0) is satisfied
        Assert.Equal(0, colomboStock.Quantity);
        Assert.Equal(65, galleStock.Quantity);
        Assert.Equal(20, kandyStock.Quantity);

        // Total stock preserved: 0 + 65 + 20 = 85
        Assert.Equal(85, colomboStock.Quantity + galleStock.Quantity + kandyStock.Quantity);

        var transfer = await verifyContext.Transfers.SingleAsync();
        Assert.Equal(50, transfer.Quantity);
    }

    // =========================================================================
    // Area 3: Insufficient Stock
    // =========================================================================

    [Fact]
    public async Task InsufficientStock_RequestedExceedsAvailable_RejectsWithConflictAndLeavesDataUnchanged()
    {
        await using var testDb = await SqliteTestDatabase.CreateAsync();
        using var context = testDb.CreateContext();
        var service = new TransferService(context, NullLogger<TransferService>.Instance);

        // Colombo has 50 Rice. Request 51 units (exceeds available by 1).
        var request = new CreateTransferRequest(FromBranchId: 1, ToBranchId: 2, ProductId: 1, Quantity: 51);
        var result = await service.CreateTransferAsync(request);

        // 1. Verify conflict outcome and informative message
        Assert.False(result.IsSuccess);
        Assert.Equal(TransferErrorType.InventoryConflict, result.ErrorType);
        Assert.Contains("Insufficient stock", result.ErrorMessage);
        Assert.Contains("Available: 50, requested: 51", result.ErrorMessage);

        // 2. Verify all stocks remain pristine
        using var verifyContext = testDb.CreateContext();
        var colomboRice = await verifyContext.Stocks.SingleAsync(s => s.BranchId == 1 && s.ProductId == 1);
        var kandyRice = await verifyContext.Stocks.SingleAsync(s => s.BranchId == 2 && s.ProductId == 1);
        var galleRice = await verifyContext.Stocks.SingleAsync(s => s.BranchId == 3 && s.ProductId == 1);

        Assert.Equal(50, colomboRice.Quantity);
        Assert.Equal(20, kandyRice.Quantity);
        Assert.Equal(15, galleRice.Quantity);
        Assert.Equal(85, colomboRice.Quantity + kandyRice.Quantity + galleRice.Quantity);

        // 3. No transfer record persisted
        Assert.Empty(await verifyContext.Transfers.ToListAsync());
    }

    // =========================================================================
    // Area 4: Same Branch
    // =========================================================================

    [Fact]
    public async Task SameBranch_SourceEqualsDestination_RejectsWithInvalidRequestAndLeavesDataUnchanged()
    {
        await using var testDb = await SqliteTestDatabase.CreateAsync();
        using var context = testDb.CreateContext();
        var service = new TransferService(context, NullLogger<TransferService>.Instance);

        var request = new CreateTransferRequest(FromBranchId: 1, ToBranchId: 1, ProductId: 1, Quantity: 5);
        var result = await service.CreateTransferAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(TransferErrorType.InvalidRequest, result.ErrorType);
        Assert.Equal("Source and destination branches cannot be the same.", result.ErrorMessage);

        // Verify no changes to stock or transfers
        using var verifyContext = testDb.CreateContext();
        var colomboRice = await verifyContext.Stocks.SingleAsync(s => s.BranchId == 1 && s.ProductId == 1);
        Assert.Equal(50, colomboRice.Quantity);
        Assert.Empty(await verifyContext.Transfers.ToListAsync());
    }

    // =========================================================================
    // Area 5: Nonpositive Quantity
    // =========================================================================

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-50)]
    public async Task NonpositiveQuantity_ZeroOrNegative_RejectsWithInvalidRequestAndLeavesDataUnchanged(int invalidQuantity)
    {
        await using var testDb = await SqliteTestDatabase.CreateAsync();
        using var context = testDb.CreateContext();
        var service = new TransferService(context, NullLogger<TransferService>.Instance);

        var request = new CreateTransferRequest(FromBranchId: 1, ToBranchId: 2, ProductId: 1, Quantity: invalidQuantity);
        var result = await service.CreateTransferAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(TransferErrorType.InvalidRequest, result.ErrorType);
        Assert.Equal("Transfer quantity must be greater than zero.", result.ErrorMessage);

        using var verifyContext = testDb.CreateContext();
        var colomboRice = await verifyContext.Stocks.SingleAsync(s => s.BranchId == 1 && s.ProductId == 1);
        var kandyRice = await verifyContext.Stocks.SingleAsync(s => s.BranchId == 2 && s.ProductId == 1);
        Assert.Equal(50, colomboRice.Quantity);
        Assert.Equal(20, kandyRice.Quantity);
        Assert.Empty(await verifyContext.Transfers.ToListAsync());
    }

    // =========================================================================
    // Area 6: Nonexistent References
    // =========================================================================

    [Theory]
    [InlineData(999, 2, 1, "Source branch with ID 999 does not exist.")]
    [InlineData(1, 999, 1, "Destination branch with ID 999 does not exist.")]
    [InlineData(1, 2, 999, "Product with ID 999 does not exist.")]
    public async Task NonexistentReferences_MissingSourceDestinationOrProduct_RejectsWithNotFoundAndLeavesDataUnchanged(
        int fromBranchId, int toBranchId, int productId, string expectedErrorMessage)
    {
        await using var testDb = await SqliteTestDatabase.CreateAsync();
        using var context = testDb.CreateContext();
        var service = new TransferService(context, NullLogger<TransferService>.Instance);

        var request = new CreateTransferRequest(fromBranchId, toBranchId, productId, 5);
        var result = await service.CreateTransferAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(TransferErrorType.NotFound, result.ErrorType);
        Assert.Equal(expectedErrorMessage, result.ErrorMessage);

        using var verifyContext = testDb.CreateContext();
        var colomboRice = await verifyContext.Stocks.SingleAsync(s => s.BranchId == 1 && s.ProductId == 1);
        Assert.Equal(50, colomboRice.Quantity);
        Assert.Empty(await verifyContext.Transfers.ToListAsync());
    }

    // =========================================================================
    // Area 7: Inventory Safeguards
    // =========================================================================

    [Fact]
    public async Task InventorySafeguards_MissingSourceStockRow_RejectsWithConflictAndLeavesDataUnchanged()
    {
        await using var testDb = await SqliteTestDatabase.CreateAsync();

        // Simulate a corrupted or missing source stock row
        using (var setupContext = testDb.CreateContext())
        {
            var sourceRow = await setupContext.Stocks.SingleAsync(s => s.BranchId == 1 && s.ProductId == 1);
            setupContext.Stocks.Remove(sourceRow);
            await setupContext.SaveChangesAsync();
        }

        using var context = testDb.CreateContext();
        var service = new TransferService(context, NullLogger<TransferService>.Instance);

        var request = new CreateTransferRequest(FromBranchId: 1, ToBranchId: 2, ProductId: 1, Quantity: 5);
        var result = await service.CreateTransferAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(TransferErrorType.InventoryConflict, result.ErrorType);
        Assert.Contains("Source stock record does not exist", result.ErrorMessage);

        // Verify destination stock remains untouched and no transfer was persisted
        using var verifyContext = testDb.CreateContext();
        var kandyRice = await verifyContext.Stocks.SingleAsync(s => s.BranchId == 2 && s.ProductId == 1);
        Assert.Equal(20, kandyRice.Quantity);
        Assert.Empty(await verifyContext.Transfers.ToListAsync());
    }

    [Fact]
    public async Task InventorySafeguards_MissingDestinationStockRow_RejectsWithConflictAndLeavesDataUnchanged()
    {
        await using var testDb = await SqliteTestDatabase.CreateAsync();

        // Simulate a missing destination stock row
        using (var setupContext = testDb.CreateContext())
        {
            var destRow = await setupContext.Stocks.SingleAsync(s => s.BranchId == 2 && s.ProductId == 1);
            setupContext.Stocks.Remove(destRow);
            await setupContext.SaveChangesAsync();
        }

        using var context = testDb.CreateContext();
        var service = new TransferService(context, NullLogger<TransferService>.Instance);

        var request = new CreateTransferRequest(FromBranchId: 1, ToBranchId: 2, ProductId: 1, Quantity: 5);
        var result = await service.CreateTransferAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(TransferErrorType.InventoryConflict, result.ErrorType);
        Assert.Contains("Destination stock record does not exist", result.ErrorMessage);

        // Verify source stock remains untouched and no transfer was persisted
        using var verifyContext = testDb.CreateContext();
        var colomboRice = await verifyContext.Stocks.SingleAsync(s => s.BranchId == 1 && s.ProductId == 1);
        Assert.Equal(50, colomboRice.Quantity);
        Assert.Empty(await verifyContext.Transfers.ToListAsync());
    }

    [Fact]
    public async Task InventorySafeguards_DestinationIntegerOverflow_RejectsWithConflictAndLeavesDataUnchanged()
    {
        await using var testDb = await SqliteTestDatabase.CreateAsync();

        // Set destination stock to near int.MaxValue
        using (var setupContext = testDb.CreateContext())
        {
            var kandyStock = await setupContext.Stocks.SingleAsync(s => s.BranchId == 2 && s.ProductId == 1);
            kandyStock.Quantity = int.MaxValue - 2;
            await setupContext.SaveChangesAsync();
        }

        using var context = testDb.CreateContext();
        var service = new TransferService(context, NullLogger<TransferService>.Instance);

        // Attempt transfer of 5 units (int.MaxValue - 2 + 5 would exceed int.MaxValue)
        var request = new CreateTransferRequest(FromBranchId: 1, ToBranchId: 2, ProductId: 1, Quantity: 5);
        var result = await service.CreateTransferAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(TransferErrorType.InventoryConflict, result.ErrorType);
        Assert.Equal("Destination stock quantity would exceed maximum integer capacity.", result.ErrorMessage);

        // Verify source and destination stocks remain unmodified
        using var verifyContext = testDb.CreateContext();
        var colomboRice = await verifyContext.Stocks.SingleAsync(s => s.BranchId == 1 && s.ProductId == 1);
        var kandyRice = await verifyContext.Stocks.SingleAsync(s => s.BranchId == 2 && s.ProductId == 1);

        Assert.Equal(50, colomboRice.Quantity);
        Assert.Equal(int.MaxValue - 2, kandyRice.Quantity);
        Assert.Empty(await verifyContext.Transfers.ToListAsync());
    }

    // =========================================================================
    // Area 8: Rollback After a Later Failure
    // =========================================================================

    [Fact]
    public async Task Rollback_FailureWhenInsertingTransferRecord_RollsBackStockDeductionsAndLeavesDataUnchanged()
    {
        await using var testDb = await SqliteTestDatabase.CreateAsync();

        // Configure a context with FailingTransferSaveInterceptor which throws during
        // SavingChangesAsync when a Transfer entity is being added.
        var interceptor = new FailingTransferSaveInterceptor("Simulated failure during transfer persistence.");
        using var failingContext = testDb.CreateContext(interceptor);
        var service = new TransferService(failingContext, NullLogger<TransferService>.Instance);

        // Initial state: Colombo has 50 Rice, Kandy has 20 Rice. Total = 85.
        var request = new CreateTransferRequest(FromBranchId: 1, ToBranchId: 2, ProductId: 1, Quantity: 10);

        // The service executes ExecuteUpdateAsync for source deduction and destination increment,
        // but fails when calling SaveChangesAsync for the Transfer record.
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateTransferAsync(request));
        Assert.Contains("Simulated failure during transfer persistence", exception.Message);

        // In a fresh context, verify that the transaction rolled back completely:
        // Source deduction must be undone, destination increment must be undone, no transfer record exists.
        using var verifyContext = testDb.CreateContext();
        var colomboRice = await verifyContext.Stocks.SingleAsync(s => s.BranchId == 1 && s.ProductId == 1);
        var kandyRice = await verifyContext.Stocks.SingleAsync(s => s.BranchId == 2 && s.ProductId == 1);
        var galleRice = await verifyContext.Stocks.SingleAsync(s => s.BranchId == 3 && s.ProductId == 1);

        Assert.Equal(50, colomboRice.Quantity);
        Assert.Equal(20, kandyRice.Quantity);
        Assert.Equal(15, galleRice.Quantity);
        Assert.Equal(85, colomboRice.Quantity + kandyRice.Quantity + galleRice.Quantity);

        Assert.Empty(await verifyContext.Transfers.ToListAsync());
    }

    // =========================================================================
    // Area 9: Concurrent Spending
    // =========================================================================

    [Fact]
    public async Task ConcurrentSpending_TwoOverlappingRequestsExceedingAvailableStock_ExactlyOneSucceedsAndStockConserved()
    {
        await using var testDb = await SqliteTestDatabase.CreateAsync();

        // Setup: Set Colombo Rice to exactly 10 units (initial was 50)
        using (var setupContext = testDb.CreateContext())
        {
            var stock = await setupContext.Stocks.SingleAsync(s => s.BranchId == 1 && s.ProductId == 1);
            stock.Quantity = 10;
            await setupContext.SaveChangesAsync();
        }

        // Coordinate two tasks to trigger concurrently using a barrier
        var barrier = new Barrier(2);

        var task1 = Task.Run(async () =>
        {
            using var context1 = testDb.CreateContext();
            var service1 = new TransferService(context1, NullLogger<TransferService>.Instance);
            barrier.SignalAndWait();
            return await service1.CreateTransferAsync(new CreateTransferRequest(FromBranchId: 1, ToBranchId: 2, ProductId: 1, Quantity: 8));
        });

        var task2 = Task.Run(async () =>
        {
            using var context2 = testDb.CreateContext();
            var service2 = new TransferService(context2, NullLogger<TransferService>.Instance);
            barrier.SignalAndWait();
            return await service2.CreateTransferAsync(new CreateTransferRequest(FromBranchId: 1, ToBranchId: 3, ProductId: 1, Quantity: 8));
        });

        var results = await Task.WhenAll(task1, task2);

        var successCount = results.Count(r => r.IsSuccess);
        var failureCount = results.Count(r => !r.IsSuccess);

        // Exactly one request must succeed; one must fail
        Assert.Equal(1, successCount);
        Assert.Equal(1, failureCount);

        var failedResult = results.Single(r => !r.IsSuccess);
        Assert.True(
            failedResult.ErrorType is TransferErrorType.InventoryConflict or TransferErrorType.DatabaseContention,
            $"Expected InventoryConflict or DatabaseContention, got {failedResult.ErrorType} ({failedResult.ErrorMessage})");

        // Verify final database state in fresh context
        using var verifyContext = testDb.CreateContext();
        var colomboStock = await verifyContext.Stocks.SingleAsync(s => s.BranchId == 1 && s.ProductId == 1);
        var kandyStock = await verifyContext.Stocks.SingleAsync(s => s.BranchId == 2 && s.ProductId == 1);
        var galleStock = await verifyContext.Stocks.SingleAsync(s => s.BranchId == 3 && s.ProductId == 1);

        // Source ended at exactly 2 (10 - 8 = 2); must NEVER drop negative!
        Assert.Equal(2, colomboStock.Quantity);

        // Exactly 8 units credited to one destination (Kandy base was 20, Galle base was 15)
        var kandyDiff = kandyStock.Quantity - 20;
        var galleDiff = galleStock.Quantity - 15;
        Assert.Equal(8, kandyDiff + galleDiff);

        // Total stock across all branches is conserved: 2 + 20 + 15 + 8 = 45 (original was 10 + 20 + 15 = 45)
        Assert.Equal(45, colomboStock.Quantity + kandyStock.Quantity + galleStock.Quantity);

        // Exactly one transfer record in history
        var transfers = await verifyContext.Transfers.ToListAsync();
        Assert.Single(transfers);
        Assert.Equal(8, transfers[0].Quantity);
    }

    // =========================================================================
    // Area 10: History Ordering and Filtering
    // =========================================================================

    [Fact]
    public async Task TransferHistory_WhenNoTransfersOccurred_ReturnsEmptyCollection()
    {
        await using var testDb = await SqliteTestDatabase.CreateAsync();
        using var context = testDb.CreateContext();
        var service = new TransferService(context, NullLogger<TransferService>.Instance);

        var history = await service.GetTransferHistoryAsync();

        Assert.NotNull(history);
        Assert.Empty(history);
    }

    [Fact]
    public async Task TransferHistory_MultipleSuccessfulTransfers_OrderedNewestFirst()
    {
        await using var testDb = await SqliteTestDatabase.CreateAsync();
        using var context = testDb.CreateContext();
        var service = new TransferService(context, NullLogger<TransferService>.Instance);

        // Perform first transfer
        var firstResult = await service.CreateTransferAsync(new CreateTransferRequest(1, 2, 1, 5));
        Assert.True(firstResult.IsSuccess);

        // Ensure distinct timestamp
        await Task.Delay(15);

        // Perform second transfer
        var secondResult = await service.CreateTransferAsync(new CreateTransferRequest(2, 3, 1, 2));
        Assert.True(secondResult.IsSuccess);

        var history = await service.GetTransferHistoryAsync();

        Assert.Equal(2, history.Count);
        // Newest transfer must be first
        Assert.Equal(secondResult.Value!.Id, history[0].Id);
        Assert.Equal(firstResult.Value!.Id, history[1].Id);
        Assert.True(history[0].CreatedAtUtc >= history[1].CreatedAtUtc);
    }

    [Fact]
    public async Task TransferHistory_EqualTimestamps_OrdersDeterministicallyByDescendingId()
    {
        await using var testDb = await SqliteTestDatabase.CreateAsync();
        var fixedTimestamp = new DateTime(2026, 10, 27, 12, 0, 0, DateTimeKind.Utc);

        // Directly insert two transfer records with the exact same CreatedAtUtc
        using (var seedContext = testDb.CreateContext())
        {
            var transfer1 = new Transfer
            {
                FromBranchId = 1,
                ToBranchId = 2,
                ProductId = 1,
                Quantity = 5,
                CreatedAtUtc = fixedTimestamp
            };
            var transfer2 = new Transfer
            {
                FromBranchId = 2,
                ToBranchId = 3,
                ProductId = 1,
                Quantity = 3,
                CreatedAtUtc = fixedTimestamp
            };

            seedContext.Transfers.AddRange(transfer1, transfer2);
            await seedContext.SaveChangesAsync();
        }

        using var queryContext = testDb.CreateContext();
        var service = new TransferService(queryContext, NullLogger<TransferService>.Instance);
        var history = await service.GetTransferHistoryAsync();

        Assert.Equal(2, history.Count);
        Assert.Equal(fixedTimestamp, history[0].CreatedAtUtc);
        Assert.Equal(fixedTimestamp, history[1].CreatedAtUtc);

        // Deterministic secondary sort: Id descending
        Assert.True(history[0].Id > history[1].Id, "When timestamps are equal, newer Id must appear first.");
    }

    [Fact]
    public async Task TransferHistory_FailedTransferAttempts_NeverAppearInHistory()
    {
        await using var testDb = await SqliteTestDatabase.CreateAsync();
        using var context = testDb.CreateContext();
        var service = new TransferService(context, NullLogger<TransferService>.Instance);

        // 1. One successful transfer
        var successResult = await service.CreateTransferAsync(new CreateTransferRequest(1, 2, 1, 5));
        Assert.True(successResult.IsSuccess);

        // 2. Multiple failed transfer attempts
        var failInsufficient = await service.CreateTransferAsync(new CreateTransferRequest(1, 2, 1, 9999));
        Assert.False(failInsufficient.IsSuccess);

        var failSameBranch = await service.CreateTransferAsync(new CreateTransferRequest(1, 1, 1, 5));
        Assert.False(failSameBranch.IsSuccess);

        var failNonexistent = await service.CreateTransferAsync(new CreateTransferRequest(999, 2, 1, 5));
        Assert.False(failNonexistent.IsSuccess);

        var failNonpositive = await service.CreateTransferAsync(new CreateTransferRequest(1, 2, 1, 0));
        Assert.False(failNonpositive.IsSuccess);

        // 3. History must only contain the single successful transfer
        var history = await service.GetTransferHistoryAsync();
        Assert.Single(history);
        Assert.Equal(successResult.Value!.Id, history[0].Id);
        Assert.Equal(5, history[0].Quantity);
    }

    // =========================================================================
    // Controller Status Code Mapping Integration
    // =========================================================================

    [Fact]
    public async Task TransfersController_MapsServiceOutcomesToCorrectHttpResponses()
    {
        await using var testDb = await SqliteTestDatabase.CreateAsync();
        using var context = testDb.CreateContext();
        var service = new TransferService(context, NullLogger<TransferService>.Instance);
        var controller = new TransfersController(service);

        // 1. Success -> 200 OK
        var successResult = await controller.CreateTransfer(new CreateTransferRequest(1, 2, 1, 5), CancellationToken.None);
        var okResult = Assert.IsType<OkObjectResult>(successResult.Result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);

        // 2. Same branch -> 400 Bad Request
        var badRequestResult = await controller.CreateTransfer(new CreateTransferRequest(1, 1, 1, 5), CancellationToken.None);
        var objectResult400 = Assert.IsType<ObjectResult>(badRequestResult.Result);
        Assert.Equal(StatusCodes.Status400BadRequest, objectResult400.StatusCode);

        // 3. Missing branch -> 404 Not Found
        var notFoundResult = await controller.CreateTransfer(new CreateTransferRequest(999, 2, 1, 5), CancellationToken.None);
        var objectResult404 = Assert.IsType<ObjectResult>(notFoundResult.Result);
        Assert.Equal(StatusCodes.Status404NotFound, objectResult404.StatusCode);

        // 4. Insufficient stock -> 409 Conflict
        var conflictResult = await controller.CreateTransfer(new CreateTransferRequest(1, 2, 1, 9999), CancellationToken.None);
        var objectResult409 = Assert.IsType<ObjectResult>(conflictResult.Result);
        Assert.Equal(StatusCodes.Status409Conflict, objectResult409.StatusCode);
    }
}
