using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MihiraStockTransfer.Api.Data;
using MihiraStockTransfer.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Configure SQLite database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Data Source=mihira.db";

var connectionStringBuilder = new SqliteConnectionStringBuilder(connectionString);
if (!Path.IsPathRooted(connectionStringBuilder.DataSource))
{
    connectionStringBuilder.DataSource = Path.Combine(builder.Environment.ContentRootPath, connectionStringBuilder.DataSource);
}

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(connectionStringBuilder.ToString()));

builder.Services.AddScoped<DatabaseInitializer>();
builder.Services.AddScoped<IInventoryQueryService, InventoryQueryService>();
builder.Services.AddScoped<ITransferService, TransferService>();

var app = builder.Build();

// Automatically initialize and seed the database on startup
using (var scope = app.Services.CreateScope())
{
    var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
    await initializer.InitializeAsync();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
