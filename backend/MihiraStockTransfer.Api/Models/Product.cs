namespace MihiraStockTransfer.Api.Models;

public class Product
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string PackageSize { get; set; } = string.Empty;

    public ICollection<Stock> Stocks { get; set; } = new List<Stock>();

    public ICollection<Transfer> Transfers { get; set; } = new List<Transfer>();
}
