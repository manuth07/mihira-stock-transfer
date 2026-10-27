using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using MihiraStockTransfer.Api.Data;

namespace MihiraStockTransfer.Api.Tests.Infrastructure;

/// <summary>
/// Provides an isolated, temporary, file-backed SQLite database for integration testing.
/// Uses a unique directory and file per instance with connection pooling disabled
/// to ensure complete test isolation and deterministic cleanup without file locking.
/// </summary>
public sealed class SqliteTestDatabase : IAsyncDisposable, IDisposable
{
    private readonly string _directoryPath;
    private readonly string _filePath;
    private readonly string _connectionString;
    private readonly DbContextOptions<AppDbContext> _baseOptions;
    private readonly List<AppDbContext> _activeContexts = new();
    private readonly object _lock = new();
    private bool _disposed;

    private SqliteTestDatabase(
        string directoryPath,
        string filePath,
        string connectionString,
        DbContextOptions<AppDbContext> baseOptions)
    {
        _directoryPath = directoryPath;
        _filePath = filePath;
        _connectionString = connectionString;
        _baseOptions = baseOptions;
    }

    public string ConnectionString => _connectionString;
    public string DatabasePath => _filePath;
    public DbContextOptions<AppDbContext> Options => _baseOptions;

    /// <summary>
    /// Creates a unique temporary file-backed SQLite database, creates the schema,
    /// runs startup seeding via DatabaseInitializer, and returns the helper.
    /// </summary>
    public static async Task<SqliteTestDatabase> CreateAsync(params IInterceptor[] interceptors)
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), "MihiraStockTransferTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directoryPath);
        var filePath = Path.Combine(directoryPath, "mihira_test.db");

        // Pooling=False disables Microsoft.Data.Sqlite connection pooling so file locks
        // are released as soon as connections are disposed.
        var connectionString = $"Data Source={filePath};Pooling=False";

        var builder = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connectionString);

        if (interceptors is { Length: > 0 })
        {
            builder.AddInterceptors(interceptors);
        }

        var options = builder.Options;

        // Initialize schema and standard seed data
        await using (var context = new AppDbContext(options))
        {
            var initializer = new DatabaseInitializer(context, NullLogger<DatabaseInitializer>.Instance);
            await initializer.InitializeAsync();
        }

        return new SqliteTestDatabase(directoryPath, filePath, connectionString, options);
    }

    /// <summary>
    /// Returns a new DbContext instance connected to this isolated test database.
    /// </summary>
    public AppDbContext CreateContext()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var context = new AppDbContext(_baseOptions);
        lock (_lock)
        {
            _activeContexts.Add(context);
        }
        return context;
    }

    /// <summary>
    /// Returns a new DbContext instance connected to this isolated test database
    /// configured with specific EF Core interceptors (e.g. for simulating failures).
    /// </summary>
    public AppDbContext CreateContext(params IInterceptor[] interceptors)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var builder = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connectionString);

        if (interceptors is { Length: > 0 })
        {
            builder.AddInterceptors(interceptors);
        }

        var context = new AppDbContext(builder.Options);
        lock (_lock)
        {
            _activeContexts.Add(context);
        }
        return context;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // Dispose any active contexts that were tracked
        List<AppDbContext> contextsToDispose;
        lock (_lock)
        {
            contextsToDispose = new List<AppDbContext>(_activeContexts);
            _activeContexts.Clear();
        }

        foreach (var ctx in contextsToDispose)
        {
            try
            {
                await ctx.DisposeAsync();
            }
            catch
            {
                // Best-effort context disposal
            }
        }

        // Flush SQLite connection pools
        SqliteConnection.ClearAllPools();

        // Remove the temporary directory and database file
        try
        {
            if (Directory.Exists(_directoryPath))
            {
                Directory.Delete(_directoryPath, recursive: true);
            }
        }
        catch
        {
            // Best-effort directory cleanup on Windows file systems
        }
    }

    public void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}
