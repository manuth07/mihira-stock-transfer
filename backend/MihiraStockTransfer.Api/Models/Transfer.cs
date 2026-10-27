namespace MihiraStockTransfer.Api.Models;

public class Transfer
{
    public int Id { get; set; }

    public int FromBranchId { get; set; }

    public Branch FromBranch { get; set; } = null!;

    public int ToBranchId { get; set; }

    public Branch ToBranch { get; set; } = null!;

    public int ProductId { get; set; }

    public Product Product { get; set; } = null!;

    public int Quantity { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
