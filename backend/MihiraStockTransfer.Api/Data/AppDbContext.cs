using Microsoft.EntityFrameworkCore;
using MihiraStockTransfer.Api.Models;

namespace MihiraStockTransfer.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Stock> Stocks => Set<Stock>();
    public DbSet<Transfer> Transfers => Set<Transfer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Branch>(entity =>
        {
            entity.HasKey(b => b.Id);

            entity.Property(b => b.Name)
                .IsRequired()
                .HasMaxLength(100);
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(p => p.Id);

            entity.Property(p => p.Name)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(p => p.PackageSize)
                .IsRequired()
                .HasMaxLength(50);
        });

        modelBuilder.Entity<Stock>(entity =>
        {
            entity.HasKey(s => new { s.BranchId, s.ProductId });

            entity.Property(s => s.Quantity)
                .IsRequired();

            entity.HasOne(s => s.Branch)
                .WithMany(b => b.Stocks)
                .HasForeignKey(s => s.BranchId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(s => s.Product)
                .WithMany(p => p.Stocks)
                .HasForeignKey(s => s.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Stock_Quantity_NonNegative", "Quantity >= 0");
            });
        });

        modelBuilder.Entity<Transfer>(entity =>
        {
            entity.HasKey(t => t.Id);

            entity.Property(t => t.Quantity)
                .IsRequired();

            entity.Property(t => t.CreatedAtUtc)
                .IsRequired();

            entity.HasOne(t => t.FromBranch)
                .WithMany(b => b.OutgoingTransfers)
                .HasForeignKey(t => t.FromBranchId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(t => t.ToBranch)
                .WithMany(b => b.IncomingTransfers)
                .HasForeignKey(t => t.ToBranchId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(t => t.Product)
                .WithMany(p => p.Transfers)
                .HasForeignKey(t => t.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Transfer_Quantity_Positive", "Quantity > 0");
                t.HasCheckConstraint("CK_Transfer_DifferentBranches", "FromBranchId != ToBranchId");
            });
        });
    }
}
