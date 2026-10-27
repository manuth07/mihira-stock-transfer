using Microsoft.EntityFrameworkCore;
using MihiraStockTransfer.Api.Contracts.Responses;
using MihiraStockTransfer.Api.Data;

namespace MihiraStockTransfer.Api.Services;

public class InventoryQueryService : IInventoryQueryService
{
    private readonly AppDbContext _context;

    public InventoryQueryService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<BranchResponse>> GetBranchesAsync(
        CancellationToken cancellationToken = default)
    {
        return await _context.Branches
            .AsNoTracking()
            .OrderBy(b => b.Id)
            .Select(b => new BranchResponse(b.Id, b.Name))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProductResponse>> GetProductsAsync(
        CancellationToken cancellationToken = default)
    {
        return await _context.Products
            .AsNoTracking()
            .OrderBy(p => p.Id)
            .Select(p => new ProductResponse(p.Id, p.Name, p.PackageSize))
            .ToListAsync(cancellationToken);
    }

    public async Task<BranchStockResponse?> GetBranchStockAsync(
        int branchId,
        CancellationToken cancellationToken = default)
    {
        var branch = await _context.Branches
            .AsNoTracking()
            .Where(b => b.Id == branchId)
            .Select(b => new { b.Id, b.Name })
            .FirstOrDefaultAsync(cancellationToken);

        if (branch is null)
        {
            return null;
        }

        var items = await _context.Stocks
            .AsNoTracking()
            .Where(s => s.BranchId == branchId)
            .OrderBy(s => s.ProductId)
            .Select(s => new StockItemResponse(
                s.ProductId,
                s.Product.Name,
                s.Product.PackageSize,
                s.Quantity))
            .ToListAsync(cancellationToken);

        return new BranchStockResponse(branch.Id, branch.Name, items);
    }
}
