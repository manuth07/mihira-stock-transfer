namespace MihiraStockTransfer.Api.Models;

public class Stock
{
    public int BranchId { get; set; }

    public Branch Branch { get; set; } = null!;

    public int ProductId { get; set; }

    public Product Product { get; set; } = null!;

    public int Quantity { get; set; }
}
