using Microsoft.EntityFrameworkCore;
using MihiraStockTransfer.Api.Models;

namespace MihiraStockTransfer.Api.Data;

public class DatabaseInitializer
{
    private static readonly (int Id, string Name)[] PredefinedBranches =
    [
        (1, "Colombo"),
        (2, "Kandy"),
        (3, "Galle")
    ];

    private static readonly (int Id, string Name, string PackageSize)[] PredefinedProducts =
    [
        (1, "Rice", "1 kg bag"),
        (2, "Sugar", "1 kg bag"),
        (3, "Milk", "1 L carton"),
        (4, "Tea", "200 g packet"),
        (5, "Biscuits", "100 g packet")
    ];

    private static readonly (int BranchId, int ProductId, int Quantity)[] PredefinedStocks =
    [
        // Colombo
        (1, 1, 50),
        (1, 2, 40),
        (1, 3, 30),
        (1, 4, 20),
        (1, 5, 60),

        // Kandy
        (2, 1, 20),
        (2, 2, 25),
        (2, 3, 15),
        (2, 4, 10),
        (2, 5, 30),

        // Galle
        (3, 1, 15),
        (3, 2, 20),
        (3, 3, 20),
        (3, 4, 15),
        (3, 5, 25)
    ];

    private readonly AppDbContext _context;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(AppDbContext context, ILogger<DatabaseInitializer> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Ensuring SQLite database schema is created...");
        await _context.Database.EnsureCreatedAsync(cancellationToken);

        await SeedBranchesAsync(cancellationToken);
        await SeedProductsAsync(cancellationToken);
        await SeedStocksAsync(cancellationToken);

        if (_context.ChangeTracker.HasChanges())
        {
            _logger.LogInformation("Saving seeded reference data and initial stock levels...");
            await _context.SaveChangesAsync(cancellationToken);
        }

        _logger.LogInformation("Database initialization completed successfully.");
    }

    private async Task SeedBranchesAsync(CancellationToken cancellationToken)
    {
        var existingBranchIds = await _context.Branches
            .Select(b => b.Id)
            .ToHashSetAsync(cancellationToken);

        foreach (var (id, name) in PredefinedBranches)
        {
            if (!existingBranchIds.Contains(id))
            {
                _context.Branches.Add(new Branch
                {
                    Id = id,
                    Name = name
                });
            }
        }
    }

    private async Task SeedProductsAsync(CancellationToken cancellationToken)
    {
        var existingProductIds = await _context.Products
            .Select(p => p.Id)
            .ToHashSetAsync(cancellationToken);

        foreach (var (id, name, packageSize) in PredefinedProducts)
        {
            if (!existingProductIds.Contains(id))
            {
                _context.Products.Add(new Product
                {
                    Id = id,
                    Name = name,
                    PackageSize = packageSize
                });
            }
        }
    }

    private async Task SeedStocksAsync(CancellationToken cancellationToken)
    {
        var existingStockKeys = await _context.Stocks
            .Select(s => new { s.BranchId, s.ProductId })
            .ToListAsync(cancellationToken);

        var existingStockSet = existingStockKeys
            .Select(k => (k.BranchId, k.ProductId))
            .ToHashSet();

        foreach (var (branchId, productId, quantity) in PredefinedStocks)
        {
            if (!existingStockSet.Contains((branchId, productId)))
            {
                _context.Stocks.Add(new Stock
                {
                    BranchId = branchId,
                    ProductId = productId,
                    Quantity = quantity
                });
            }
        }
    }
}
