using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MihiraStockTransfer.Api.Data;
using MihiraStockTransfer.Api.Models;

namespace MihiraStockTransfer.Api.Tests;

public class DatabaseInitializationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _contextOptions;

    public DatabaseInitializationTests()
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

    private DatabaseInitializer CreateInitializer(AppDbContext context) =>
        new(context, NullLogger<DatabaseInitializer>.Instance);

    [Fact]
    public async Task InitializeAsync_OnEmptyDatabase_SeedsExpectedData()
    {
        using var context = CreateContext();
        var initializer = CreateInitializer(context);

        await initializer.InitializeAsync();

        // 1. Verify Branches
        var branches = await context.Branches.OrderBy(b => b.Id).ToListAsync();
        Assert.Equal(3, branches.Count);
        Assert.Equal((1, "Colombo"), (branches[0].Id, branches[0].Name));
        Assert.Equal((2, "Kandy"), (branches[1].Id, branches[1].Name));
        Assert.Equal((3, "Galle"), (branches[2].Id, branches[2].Name));

        // 2. Verify Products
        var products = await context.Products.OrderBy(p => p.Id).ToListAsync();
        Assert.Equal(5, products.Count);
        Assert.Equal((1, "Rice", "1 kg bag"), (products[0].Id, products[0].Name, products[0].PackageSize));
        Assert.Equal((2, "Sugar", "1 kg bag"), (products[1].Id, products[1].Name, products[1].PackageSize));
        Assert.Equal((3, "Milk", "1 L carton"), (products[2].Id, products[2].Name, products[2].PackageSize));
        Assert.Equal((4, "Tea", "200 g packet"), (products[3].Id, products[3].Name, products[3].PackageSize));
        Assert.Equal((5, "Biscuits", "100 g packet"), (products[4].Id, products[4].Name, products[4].PackageSize));

        // 3. Verify Stocks (15 combinations)
        var stocks = await context.Stocks.ToListAsync();
        Assert.Equal(15, stocks.Count);

        // Colombo: Rice: 50, Sugar: 40, Milk: 30, Tea: 20, Biscuits: 60
        Assert.Equal(50, stocks.Single(s => s.BranchId == 1 && s.ProductId == 1).Quantity);
        Assert.Equal(40, stocks.Single(s => s.BranchId == 1 && s.ProductId == 2).Quantity);
        Assert.Equal(30, stocks.Single(s => s.BranchId == 1 && s.ProductId == 3).Quantity);
        Assert.Equal(20, stocks.Single(s => s.BranchId == 1 && s.ProductId == 4).Quantity);
        Assert.Equal(60, stocks.Single(s => s.BranchId == 1 && s.ProductId == 5).Quantity);

        // Kandy: Rice: 20, Sugar: 25, Milk: 15, Tea: 10, Biscuits: 30
        Assert.Equal(20, stocks.Single(s => s.BranchId == 2 && s.ProductId == 1).Quantity);
        Assert.Equal(25, stocks.Single(s => s.BranchId == 2 && s.ProductId == 2).Quantity);
        Assert.Equal(15, stocks.Single(s => s.BranchId == 2 && s.ProductId == 3).Quantity);
        Assert.Equal(10, stocks.Single(s => s.BranchId == 2 && s.ProductId == 4).Quantity);
        Assert.Equal(30, stocks.Single(s => s.BranchId == 2 && s.ProductId == 5).Quantity);

        // Galle: Rice: 15, Sugar: 20, Milk: 20, Tea: 15, Biscuits: 25
        Assert.Equal(15, stocks.Single(s => s.BranchId == 3 && s.ProductId == 1).Quantity);
        Assert.Equal(20, stocks.Single(s => s.BranchId == 3 && s.ProductId == 2).Quantity);
        Assert.Equal(20, stocks.Single(s => s.BranchId == 3 && s.ProductId == 3).Quantity);
        Assert.Equal(15, stocks.Single(s => s.BranchId == 3 && s.ProductId == 4).Quantity);
        Assert.Equal(25, stocks.Single(s => s.BranchId == 3 && s.ProductId == 5).Quantity);

        // 4. Verify Transfers (0 transfers seeded)
        var transfers = await context.Transfers.ToListAsync();
        Assert.Empty(transfers);
    }

    [Fact]
    public async Task InitializeAsync_WhenRunMultipleTimes_IsIdempotentAndDoesNotDuplicate()
    {
        using var context = CreateContext();
        var initializer = CreateInitializer(context);

        // Run first time
        await initializer.InitializeAsync();

        // Run second time
        await initializer.InitializeAsync();

        Assert.Equal(3, await context.Branches.CountAsync());
        Assert.Equal(5, await context.Products.CountAsync());
        Assert.Equal(15, await context.Stocks.CountAsync());
        Assert.Equal(0, await context.Transfers.CountAsync());
    }

    [Fact]
    public async Task InitializeAsync_PreservesModifiedStockQuantity()
    {
        using var context = CreateContext();
        var initializer = CreateInitializer(context);

        // Run initial seed
        await initializer.InitializeAsync();

        // Simulate a stock transfer or modification: Colombo Rice changed from 50 to 42
        var colomboRice = await context.Stocks.SingleAsync(s => s.BranchId == 1 && s.ProductId == 1);
        colomboRice.Quantity = 42;
        await context.SaveChangesAsync();

        // Run initializer again (simulating application restart)
        using var restartContext = CreateContext();
        var restartInitializer = CreateInitializer(restartContext);
        await restartInitializer.InitializeAsync();

        // Verify the modified quantity was NOT overwritten with 50
        var reloadedStock = await restartContext.Stocks.SingleAsync(s => s.BranchId == 1 && s.ProductId == 1);
        Assert.Equal(42, reloadedStock.Quantity);
    }

    [Fact]
    public async Task Stock_CheckConstraint_PreventsNegativeQuantity()
    {
        using var context = CreateContext();
        var initializer = CreateInitializer(context);
        await initializer.InitializeAsync();

        // Modifying quantity to negative should violate CK_Stock_Quantity_NonNegative
        var stock = await context.Stocks.SingleAsync(s => s.BranchId == 1 && s.ProductId == 1);
        stock.Quantity = -1;

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task Transfer_CheckConstraint_RequiresPositiveQuantity()
    {
        using var context = CreateContext();
        var initializer = CreateInitializer(context);
        await initializer.InitializeAsync();

        var transferWithZero = new Transfer
        {
            FromBranchId = 1,
            ToBranchId = 2,
            ProductId = 1,
            Quantity = 0,
            CreatedAtUtc = DateTime.UtcNow
        };

        context.Transfers.Add(transferWithZero);

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task Transfer_CheckConstraint_PreventsTransferToSameBranch()
    {
        using var context = CreateContext();
        var initializer = CreateInitializer(context);
        await initializer.InitializeAsync();

        var transferToSameBranch = new Transfer
        {
            FromBranchId = 1,
            ToBranchId = 1,
            ProductId = 1,
            Quantity = 5,
            CreatedAtUtc = DateTime.UtcNow
        };

        context.Transfers.Add(transferToSameBranch);

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task ActualDatabaseFile_ContainsExpectedInitialData()
    {
        // Check the actual mihira.db file created when the API runs
        var possiblePaths = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "../../../../../backend/MihiraStockTransfer.Api/mihira.db"),
            Path.Combine(AppContext.BaseDirectory, "../../../../MihiraStockTransfer.Api/mihira.db"),
            "E:/mihira-stock-transfer/backend/MihiraStockTransfer.Api/mihira.db"
        };

        var actualDbPath = possiblePaths.FirstOrDefault(File.Exists);
        if (actualDbPath is null)
        {
            // If mihira.db is not yet created on disk, skip this verification
            return;
        }

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={actualDbPath}")
            .Options;

        using var context = new AppDbContext(options);

        // Verify branches
        Assert.Equal(3, await context.Branches.CountAsync());
        // Verify products
        Assert.Equal(5, await context.Products.CountAsync());
        // Verify stocks
        Assert.Equal(15, await context.Stocks.CountAsync());
        // Verify transfers
        Assert.Equal(0, await context.Transfers.CountAsync());

        // Verify specific stock quantities
        var colomboRice = await context.Stocks.SingleAsync(s => s.BranchId == 1 && s.ProductId == 1);
        Assert.Equal(50, colomboRice.Quantity);

        var galleBiscuits = await context.Stocks.SingleAsync(s => s.BranchId == 3 && s.ProductId == 5);
        Assert.Equal(25, galleBiscuits.Quantity);
    }
}
