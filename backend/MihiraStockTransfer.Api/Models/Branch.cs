namespace MihiraStockTransfer.Api.Models;

public class Branch
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public ICollection<Stock> Stocks { get; set; } = new List<Stock>();

    public ICollection<Transfer> OutgoingTransfers { get; set; } = new List<Transfer>();

    public ICollection<Transfer> IncomingTransfers { get; set; } = new List<Transfer>();
}
