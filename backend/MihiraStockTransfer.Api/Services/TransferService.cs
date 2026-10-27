using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MihiraStockTransfer.Api.Contracts.Requests;
using MihiraStockTransfer.Api.Contracts.Responses;
using MihiraStockTransfer.Api.Data;
using MihiraStockTransfer.Api.Models;

namespace MihiraStockTransfer.Api.Services;

public class TransferService : ITransferService
{
    private readonly AppDbContext _context;
    private readonly ILogger<TransferService> _logger;

    public TransferService(AppDbContext context, ILogger<TransferService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<TransferResult> CreateTransferAsync(
        CreateTransferRequest request,
        CancellationToken cancellationToken = default)
    {
        // 1. Business validation prior to database interaction
        if (request.Quantity <= 0)
        {
            return TransferResult.InvalidRequest("Transfer quantity must be greater than zero.");
        }

        if (request.FromBranchId == request.ToBranchId)
        {
            return TransferResult.InvalidRequest("Source and destination branches cannot be the same.");
        }

        try
        {
            // 2. Explicit database transaction covering updates and insert
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            // 3. Confirm reference data exists
            var fromBranch = await _context.Branches
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.Id == request.FromBranchId, cancellationToken);

            if (fromBranch is null)
            {
                return TransferResult.NotFound($"Source branch with ID {request.FromBranchId} does not exist.");
            }

            var toBranch = await _context.Branches
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.Id == request.ToBranchId, cancellationToken);

            if (toBranch is null)
            {
                return TransferResult.NotFound($"Destination branch with ID {request.ToBranchId} does not exist.");
            }

            var product = await _context.Products
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == request.ProductId, cancellationToken);

            if (product is null)
            {
                return TransferResult.NotFound($"Product with ID {request.ProductId} does not exist.");
            }

            // 4. Validate source and destination stock existence and availability
            var sourceStock = await _context.Stocks
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    s => s.BranchId == request.FromBranchId && s.ProductId == request.ProductId,
                    cancellationToken);

            if (sourceStock is null)
            {
                return TransferResult.InventoryConflict(
                    $"Source stock record does not exist for product '{product.Name}' at branch '{fromBranch.Name}'.");
            }

            if (sourceStock.Quantity < request.Quantity)
            {
                return TransferResult.InventoryConflict(
                    $"Insufficient stock for product '{product.Name}' at branch '{fromBranch.Name}'. Available: {sourceStock.Quantity}, requested: {request.Quantity}.");
            }

            var destinationStock = await _context.Stocks
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    s => s.BranchId == request.ToBranchId && s.ProductId == request.ProductId,
                    cancellationToken);

            if (destinationStock is null)
            {
                return TransferResult.InventoryConflict(
                    $"Destination stock record does not exist for product '{product.Name}' at branch '{toBranch.Name}'.");
            }

            if ((long)destinationStock.Quantity + request.Quantity > int.MaxValue)
            {
                return TransferResult.InventoryConflict(
                    "Destination stock quantity would exceed maximum integer capacity.");
            }

            // 5. Atomic conditional deduction on source stock
            var sourceRowsAffected = await _context.Stocks
                .Where(s => s.BranchId == request.FromBranchId
                         && s.ProductId == request.ProductId
                         && s.Quantity >= request.Quantity)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(s => s.Quantity, s => s.Quantity - request.Quantity),
                    cancellationToken);

            if (sourceRowsAffected != 1)
            {
                await transaction.RollbackAsync(cancellationToken);
                return TransferResult.InventoryConflict(
                    $"Insufficient stock or concurrent modification for product '{product.Name}' at branch '{fromBranch.Name}'.");
            }

            // 6. Increment destination stock
            var destinationRowsAffected = await _context.Stocks
                .Where(s => s.BranchId == request.ToBranchId
                         && s.ProductId == request.ProductId)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(s => s.Quantity, s => s.Quantity + request.Quantity),
                    cancellationToken);

            if (destinationRowsAffected != 1)
            {
                await transaction.RollbackAsync(cancellationToken);
                return TransferResult.InventoryConflict(
                    $"Could not update destination stock for product '{product.Name}' at branch '{toBranch.Name}'.");
            }

            // 7. Record completed transfer with server UTC timestamp
            var transfer = new Transfer
            {
                FromBranchId = request.FromBranchId,
                ToBranchId = request.ToBranchId,
                ProductId = request.ProductId,
                Quantity = request.Quantity,
                CreatedAtUtc = DateTime.UtcNow
            };

            _context.Transfers.Add(transfer);
            await _context.SaveChangesAsync(cancellationToken);

            // 8. Commit transaction
            await transaction.CommitAsync(cancellationToken);

            var response = new TransferResponse(
                transfer.Id,
                request.FromBranchId,
                fromBranch.Name,
                request.ToBranchId,
                toBranch.Name,
                request.ProductId,
                product.Name,
                product.PackageSize,
                transfer.Quantity,
                transfer.CreatedAtUtc);

            return TransferResult.Success(response);
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode is 5 or 6)
        {
            _logger.LogWarning(ex, "SQLite database contention (busy/locked) during transfer creation.");
            return TransferResult.DatabaseContention(
                "The database is currently busy processing concurrent operations. Please retry shortly.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqliteException { SqliteErrorCode: 5 or 6 })
        {
            _logger.LogWarning(ex, "SQLite database contention (busy/locked) during transfer creation.");
            return TransferResult.DatabaseContention(
                "The database is currently busy processing concurrent operations. Please retry shortly.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
    }

    public async Task<IReadOnlyList<TransferResponse>> GetTransferHistoryAsync(
        CancellationToken cancellationToken = default)
    {
        return await _context.Transfers
            .AsNoTracking()
            .OrderByDescending(t => t.CreatedAtUtc)
            .ThenByDescending(t => t.Id)
            .Select(t => new TransferResponse(
                t.Id,
                t.FromBranchId,
                t.FromBranch.Name,
                t.ToBranchId,
                t.ToBranch.Name,
                t.ProductId,
                t.Product.Name,
                t.Product.PackageSize,
                t.Quantity,
                t.CreatedAtUtc))
            .ToListAsync(cancellationToken);
    }
}
