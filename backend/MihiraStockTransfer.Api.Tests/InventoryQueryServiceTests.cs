using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MihiraStockTransfer.Api.Controllers;
using MihiraStockTransfer.Api.Data;
using MihiraStockTransfer.Api.Models;
using MihiraStockTransfer.Api.Services;

namespace MihiraStockTransfer.Api.Tests;

public class InventoryQueryServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _contextOptions;

    public InventoryQueryServiceTests()
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
    public async Task GetBranchesAsync_ReturnsAllBranchesOrderedById()
    {
        await SeedDatabaseAsync();
        using var context = CreateContext();
        var service = new InventoryQueryService(context);

        var result = await service.GetBranchesAsync();

        Assert.Equal(3, result.Count);
        Assert.Equal(1, result[0].Id);
        Assert.Equal("Colombo", result[0].Name);
        Assert.Equal(2, result[1].Id);
        Assert.Equal("Kandy", result[1].Name);
        Assert.Equal(3, result[2].Id);
        Assert.Equal("Galle", result[2].Name);
    }

    [Fact]
    public async Task GetProductsAsync_ReturnsAllProductsOrderedById()
    {
        await SeedDatabaseAsync();
        using var context = CreateContext();
        var service = new InventoryQueryService(context);

        var result = await service.GetProductsAsync();

        Assert.Equal(5, result.Count);
        Assert.Equal(1, result[0].Id);
        Assert.Equal("Rice", result[0].Name);
        Assert.Equal("1 kg bag", result[0].PackageSize);

        Assert.Equal(2, result[1].Id);
        Assert.Equal("Sugar", result[1].Name);
        Assert.Equal("1 kg bag", result[1].PackageSize);

        Assert.Equal(5, result[4].Id);
        Assert.Equal("Biscuits", result[4].Name);
        Assert.Equal("100 g packet", result[4].PackageSize);
    }

    [Fact]
    public async Task GetBranchStockAsync_ExistingBranch_ReturnsStockItemsOrderedByProductId()
    {
        await SeedDatabaseAsync();
        using var context = CreateContext();
        var service = new InventoryQueryService(context);

        // Check Colombo (Branch 1)
        var result = await service.GetBranchStockAsync(1);

        Assert.NotNull(result);
        Assert.Equal(1, result.BranchId);
        Assert.Equal("Colombo", result.BranchName);
        Assert.Equal(5, result.Items.Count);

        // Verify ordering by ProductId and contents
        Assert.Equal(1, result.Items[0].ProductId);
        Assert.Equal("Rice", result.Items[0].ProductName);
        Assert.Equal("1 kg bag", result.Items[0].PackageSize);
        Assert.Equal(50, result.Items[0].Quantity);

        Assert.Equal(2, result.Items[1].ProductId);
        Assert.Equal("Sugar", result.Items[1].ProductName);
        Assert.Equal(40, result.Items[1].Quantity);

        Assert.Equal(3, result.Items[2].ProductId);
        Assert.Equal("Milk", result.Items[2].ProductName);
        Assert.Equal(30, result.Items[2].Quantity);

        Assert.Equal(4, result.Items[3].ProductId);
        Assert.Equal("Tea", result.Items[3].ProductName);
        Assert.Equal(20, result.Items[3].Quantity);

        Assert.Equal(5, result.Items[4].ProductId);
        Assert.Equal("Biscuits", result.Items[4].ProductName);
        Assert.Equal(60, result.Items[4].Quantity);
    }

    [Fact]
    public async Task GetBranchStockAsync_NonExistentBranch_ReturnsNull()
    {
        await SeedDatabaseAsync();
        using var context = CreateContext();
        var service = new InventoryQueryService(context);

        var result = await service.GetBranchStockAsync(999);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetBranchStockAsync_IncludesZeroQuantityStock()
    {
        await SeedDatabaseAsync();
        using (var setupContext = CreateContext())
        {
            // Set Colombo Rice quantity to 0
            var stock = await setupContext.Stocks.SingleAsync(s => s.BranchId == 1 && s.ProductId == 1);
            stock.Quantity = 0;
            await setupContext.SaveChangesAsync();
        }

        using var context = CreateContext();
        var service = new InventoryQueryService(context);

        var result = await service.GetBranchStockAsync(1);

        Assert.NotNull(result);
        var zeroStock = result.Items.Single(i => i.ProductId == 1);
        Assert.Equal(0, zeroStock.Quantity);
    }

    [Fact]
    public async Task GetBranchStockAsync_ExistingBranchWithNoStock_ReturnsEmptyItems()
    {
        await SeedDatabaseAsync();
        using (var setupContext = CreateContext())
        {
            // Add a new branch with no stock rows
            setupContext.Branches.Add(new Branch { Id = 4, Name = "Jaffna" });
            await setupContext.SaveChangesAsync();
        }

        using var context = CreateContext();
        var service = new InventoryQueryService(context);

        var result = await service.GetBranchStockAsync(4);

        Assert.NotNull(result);
        Assert.Equal(4, result.BranchId);
        Assert.Equal("Jaffna", result.BranchName);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task ReadOperations_DoNotMutateDatabase()
    {
        await SeedDatabaseAsync();
        using var context = CreateContext();
        var service = new InventoryQueryService(context);

        // Perform read queries
        await service.GetBranchesAsync();
        await service.GetProductsAsync();
        await service.GetBranchStockAsync(1);

        // Verify change tracker has 0 changes
        Assert.False(context.ChangeTracker.HasChanges());
        Assert.Equal(0, await context.Transfers.CountAsync());
    }

    [Fact]
    public async Task BranchesController_GetBranchStock_Returns404WithProblemDetails_WhenNotFound()
    {
        await SeedDatabaseAsync();
        using var context = CreateContext();
        var service = new InventoryQueryService(context);
        var controller = new BranchesController(service);

        var actionResult = await controller.GetBranchStock(999, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(actionResult.Result);
        Assert.Equal(StatusCodes.Status404NotFound, objectResult.StatusCode);

        var problemDetails = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal("Branch not found", problemDetails.Title);
        Assert.Equal(StatusCodes.Status404NotFound, problemDetails.Status);
        Assert.Equal("Branch with ID 999 does not exist.", problemDetails.Detail);
    }

    [Fact]
    public async Task BranchesController_GetBranches_ReturnsOkWithBranches()
    {
        await SeedDatabaseAsync();
        using var context = CreateContext();
        var service = new InventoryQueryService(context);
        var controller = new BranchesController(service);

        var actionResult = await controller.GetBranches(CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);

        var branches = Assert.IsAssignableFrom<IReadOnlyList<Contracts.Responses.BranchResponse>>(okResult.Value);
        Assert.Equal(3, branches.Count);
    }

    [Fact]
    public async Task ProductsController_GetProducts_ReturnsOkWithProducts()
    {
        await SeedDatabaseAsync();
        using var context = CreateContext();
        var service = new InventoryQueryService(context);
        var controller = new ProductsController(service);

        var actionResult = await controller.GetProducts(CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);

        var products = Assert.IsAssignableFrom<IReadOnlyList<Contracts.Responses.ProductResponse>>(okResult.Value);
        Assert.Equal(5, products.Count);
    }
}
