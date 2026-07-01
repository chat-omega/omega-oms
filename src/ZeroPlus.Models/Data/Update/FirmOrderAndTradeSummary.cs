namespace ZeroPlus.Models.Data.Update;

public class FirmOrderAndTradeSummary
{
    public string? Id { get; set; }
    public int Index { get; set; }
    public OrderAndTradeSummary? BuySummary { get; set; }
    public OrderAndTradeSummary? SellSummary { get; set; }
}