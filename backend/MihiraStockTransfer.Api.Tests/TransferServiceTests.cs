using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MihiraStockTransfer.Api.Contracts.Requests;
using MihiraStockTransfer.Api.Controllers;
using MihiraStockTransfer.Api.Data;
using MihiraStockTransfer.Api.Services;

namespace MihiraStockTransfer.Api.Tests;

public class TransferServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _contextOptions;

    public TransferServiceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        _contextOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    private AppDbContext CreateContext() => new(_contextOptions);

    private async Task SeedDatabaseAsync()
    {
        using var context = CreateContext();
        var initializer = new DatabaseInitializer(context, NullLogger<DatabaseInitializer>.Instance);
        await initializer.InitializeAsync();
    }

    [Fact]
    public async Task CreateTransferAsync_ValidTransfer_DeductsSourceAddsDestinationAndPreservesTotal()
    {
        await SeedDatabaseAsync();
        using var context = CreateContext();
        var service = new TransferService(context, NullLogger<TransferService>.Instance);

        // Colombo has 50 Rice (Product 1), Kandy has 20, Galle has 15. Total = 85.
        var request = new CreateTransferRequest(FromBranchId: 1, ToBranchId: 2, ProductId: 1, Quantity: 10);
        var result = await service.CreateTransferAsync(request);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(1, result.Value.FromBranchId);
        Assert.Equal("Colombo", result.Value.FromBranchName);
        Assert.Equal(2, result.Value.ToBranchId);
        Assert.Equal("Kandy", result.Value.ToBranchName);
        Assert.Equal(1, result.Value.ProductId);
        Assert.Equal("Rice", result.Value.ProductName);
        Assert.Equal("1 kg bag", result.Value.PackageSize);
        Assert.Equal(10, result.Value.Quantity);

        // Verify stock levels in database
        using var verifyContext = CreateContext();
        var colomboRice = await verifyContext.Stocks.SingleAsync(s => s.BranchId == 1 && s.ProductId == 1);
        var kandyRice = await verifyContext.Stocks.SingleAsync(s => s.BranchId == 2 && s.ProductId == 1);
        var galleRice = await verifyContext.Stocks.SingleAsync(s => s.BranchId == 3 && s.ProductId == 1);

        Assert.Equal(40, colomboRice.Quantity);
        Assert.Equal(30, kandyRice.Quantity);
        Assert.Equal(15, galleRice.Quantity);

        // Total stock across all branches is preserved
        Assert.Equal(85, colomboRice.Quantity + kandyRice.Quantity + galleRice.Quantity);

        // Verify exactly one transfer record was created
        var transfers = await verifyContext.Transfers.ToListAsync();
        Assert.Single(transfers);
        Assert.Equal(10, transfers[0].Quantity);
    }

    [Fact]
    public async Task CreateTransferAsync_EntireAvailableStock_SucceedsAndLeavesZero()
    {
        await SeedDatabaseAsync();
        using var context = CreateContext();
        var service = new TransferService(context, NullLogger<TransferService>.Instance);

        // Colombo has 50 Rice. Move all 50 to Galle (initially 15).
        var request = new CreateTransferRequest(FromBranchId: 1, ToBranchId: 3, ProductId: 1, Quantity: 50);
        var result = await service.CreateTransferAsync(request);

        Assert.True(result.IsSuccess);

        using var verifyContext = CreateContext();
        var colomboStock = await verifyContext.Stocks.SingleAsync(s => s.BranchId == 1 && s.ProductId == 1);
        var galleStock = await verifyContext.Stocks.SingleAsync(s => s.BranchId == 3 && s.ProductId == 1);

        Assert.Equal(0, colomboStock.Quantity);
        Assert.Equal(65, galleStock.Quantity);
    }

    [Theory]
    [InlineData(0, 1, 2, 1, TransferErrorType.InvalidRequest)]    // Zero quantity
    [InlineData(-5, 1, 2, 1, TransferErrorType.InvalidRequest)]   // Negative quantity
    [InlineData(5, 1, 1, 1, TransferErrorType.InvalidRequest)]    // Same source and destination
    [InlineData(5, 999, 2, 1, TransferErrorType.NotFound)]        // Missing source branch
    [InlineData(5, 1, 999, 1, TransferErrorType.NotFound)]        // Missing destination branch
    [InlineData(5, 1, 2, 999, TransferErrorType.NotFound)]        // Missing product
    [InlineData(9999, 1, 2, 1, TransferErrorType.InventoryConflict)] // Insufficient stock (Colombo has 50)
    public async Task CreateTransferAsync_InvalidScenarios_FailAndLeaveDatabaseUnchanged(
        int quantity, int fromBranchId, int toBranchId, int productId, TransferErrorType expectedError)
    {
        await SeedDatabaseAsync();
        using var context = CreateContext();
        var service = new TransferService(context, NullLogger<TransferService>.Instance);

        var request = new CreateTransferRequest(fromBranchId, toBranchId, productId, quantity);
        var result = await service.CreateTransferAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(expectedError, result.ErrorType);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));

        // Confirm database was NOT modified
        using var verifyContext = CreateContext();
        var colomboRice = await verifyContext.Stocks.SingleAsync(s => s.BranchId == 1 && s.ProductId == 1);
        Assert.Equal(50, colomboRice.Quantity);
        Assert.Empty(await verifyContext.Transfers.ToListAsync());
    }

    [Fact]
    public async Task GetTransferHistoryAsync_ReturnsTransfersOrderedNewestFirst()
    {
        await SeedDatabaseAsync();
        using var context = CreateContext();
        var service = new TransferService(context, NullLogger<TransferService>.Instance);

        // Perform first transfer
        await service.CreateTransferAsync(new CreateTransferRequest(1, 2, 1, 5));
        // Small delay to ensure timestamp difference
        await Task.Delay(10);
        // Perform second transfer
        await service.CreateTransferAsync(new CreateTransferRequest(2, 3, 1, 2));

        var history = await service.GetTransferHistoryAsync();

        Assert.Equal(2, history.Count);
        // Newest transfer should be first
        Assert.True(history[0].CreatedAtUtc >= history[1].CreatedAtUtc);
        Assert.Equal(2, history[0].FromBranchId);
        Assert.Equal(3, history[0].ToBranchId);
        Assert.Equal(1, history[1].FromBranchId);
        Assert.Equal(2, history[1].ToBranchId);
    }

    [Fact]
    public async Task TransfersController_MapsServiceOutcomesToCorrectHttpResponses()
    {
        await SeedDatabaseAsync();
        using var context = CreateContext();
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

    [Fact]
    public async Task Concurrency_TwoOverlappingRequests_OnlyOneCanSpendAvailableStock()
    {
        // Use an isolated temporary file-backed SQLite database to test true multi-connection concurrency
        var tempDbPath = Path.Combine(Path.GetTempPath(), $"mihira_test_{Guid.NewGuid():N}.db");
        var connectionString = $"Data Source={tempDbPath}";

        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connectionString)
                .Options;

            // Initialize schema and initial seed
            using (var initContext = new AppDbContext(options))
            {
                var initializer = new DatabaseInitializer(initContext, NullLogger<DatabaseInitializer>.Instance);
                await initializer.InitializeAsync();

                // Set Colombo Rice to exactly 10 units
                var stock = await initContext.Stocks.SingleAsync(s => s.BranchId == 1 && s.ProductId == 1);
                stock.Quantity = 10;
                await initContext.SaveChangesAsync();
            }

            // Launch two concurrent requests attempting to transfer 8 units each
            // Both requests see 10 units initially, but only one can deduct 8 without dropping below 0
            var task1 = Task.Run(async () =>
            {
                using var context1 = new AppDbContext(options);
                var service1 = new TransferService(context1, NullLogger<TransferService>.Instance);
                return await service1.CreateTransferAsync(new CreateTransferRequest(1, 2, 1, 8));
            });

            var task2 = Task.Run(async () =>
            {
                using var context2 = new AppDbContext(options);
                var service2 = new TransferService(context2, NullLogger<TransferService>.Instance);
                return await service2.CreateTransferAsync(new CreateTransferRequest(1, 3, 1, 8));
            });

            var results = await Task.WhenAll(task1, task2);

            var successCount = results.Count(r => r.IsSuccess);
            var failureCount = results.Count(r => !r.IsSuccess);

            // Exactly one must succeed, and one must fail
            Assert.Equal(1, successCount);
            Assert.Equal(1, failureCount);

            // Verify final database state: Colombo must have exactly 2 units (10 - 8 = 2), never negative!
            using (var verifyContext = new AppDbContext(options))
            {
                var colomboStock = await verifyContext.Stocks.SingleAsync(s => s.BranchId == 1 && s.ProductId == 1);
                Assert.Equal(2, colomboStock.Quantity);

                // Verify destination credit: exactly one destination received 8 units
                var kandyStock = await verifyContext.Stocks.SingleAsync(s => s.BranchId == 2 && s.ProductId == 1);
                var galleStock = await verifyContext.Stocks.SingleAsync(s => s.BranchId == 3 && s.ProductId == 1);

                // Kandy initial 20, Galle initial 15
                var totalTransferred = (kandyStock.Quantity - 20) + (galleStock.Quantity - 15);
                Assert.Equal(8, totalTransferred);

                // Exactly one transfer record in history
                Assert.Single(await verifyContext.Transfers.ToListAsync());
            }
        }
        finally
        {
            // Clean up temporary database file
            SqliteConnection.ClearAllPools();
            if (File.Exists(tempDbPath))
            {
                try { File.Delete(tempDbPath); } catch { /* Ignore file lock during cleanup */ }
            }
        }
    }
}
