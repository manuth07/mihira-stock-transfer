using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MihiraStockTransfer.Api.Models;

namespace MihiraStockTransfer.Api.Tests.Infrastructure;

/// <summary>
/// A test-only SaveChangesInterceptor that simulates a database failure
/// when saving new Transfer entities, enabling verification of transaction
/// rollback after ExecuteUpdateAsync operations have already executed.
/// </summary>
public sealed class FailingTransferSaveInterceptor : SaveChangesInterceptor
{
    private readonly string _errorMessage;

    public FailingTransferSaveInterceptor(string errorMessage = "Simulated failure during transfer persistence.")
    {
        _errorMessage = errorMessage;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            var hasNewTransfer = eventData.Context.ChangeTracker
                .Entries<Transfer>()
                .Any(e => e.State == EntityState.Added);

            if (hasNewTransfer)
            {
                throw new InvalidOperationException(_errorMessage);
            }
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
